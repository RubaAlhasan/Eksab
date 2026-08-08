import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { HttpErrorResponse } from '@angular/common/http';
import { LocalizationPipe, PermissionService } from '@abp/ng.core';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { AdminTenantsService } from '../../proxy/controllers/admin-tenants.service';
import { CategoriesService } from '../../proxy/controllers/categories.service';
import { SupportTicketsService } from '../../proxy/controllers/support-tickets.service';
import type { AdminTenantDto } from '../../proxy/businesses/models';
import type { SupportTicketDto } from '../../proxy/platform/models';
import { TenantApprovalStatus } from '../../proxy/business-profiles/tenant-approval-status.enum';
import { SupportTicketStatus } from '../../proxy/platform/support-ticket-status.enum';
import { SupportTicketPriority } from '../../proxy/platform/support-ticket-priority.enum';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { StatusBadgeComponent, StatusBadgeVariant } from '../../shared/components/status-badge/status-badge.component';
import { LoadingSpinnerComponent } from '../../shared/components/loading-spinner/loading-spinner.component';
import { ErrorStateComponent } from '../../shared/components/error-state/error-state.component';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { PaginationComponent } from '../../shared/components/pagination/pagination.component';

type DetailTab = 'overview' | 'billing' | 'tickets';

/**
 * Admin Portal > Business Details — the routed drill-down from the Businesses list
 * (admin-tenants.component.ts), `/admin/businesses/:tenantId`. Scope matches
 * admin-portal-implementation-plan.md §04 exactly, not the prototype's business-details.html (which
 * shows fabricated stats — Points Issued/Redeemed, Active Campaigns, Members, Plan, Owner, Branches —
 * none backed by any Admin-accessible endpoint; see that doc's own gap table).
 *
 * Real API surface, nothing invented:
 * - Overview: `AdminTenantsController.GetAsync` (`AdminTenantDto` — the same fields the list page
 *   already shows) + category name (best-effort `CategoriesService.get`, read is `[AllowAnonymous]`)
 *   + one genuinely real stat, "Open Support Tickets", via a `status=Open, maxResultCount:0` filtered
 *   `SupportTicketsService.getList` (server counts, no items transferred) — omitted entirely for a
 *   viewer without `SupportTickets.Manage` rather than erroring.
 * - Billing tab: `[MISSING BACKEND CAPABILITY]` — `AdminSubscriptionFilterDto` has no `tenantId`
 *   field, so there's no way to fetch just this business's subscription server-side. Per the doc's own
 *   recommendation, this shows a "not available" empty state rather than the unscalable workaround
 *   (fetching every platform subscription and filtering client-side).
 * - Support Tickets tab: real, filtered `SupportTicketFilterDto.TenantId` — same column shape as the
 *   full Support Tickets queue (admin-support-tickets.component.ts), but read-only here (no reply/
 *   resolve actions) per the doc's own Forms/Actions sections for this page — use the full Support
 *   Tickets page for triage.
 * - No Activity Log tab — no audit log API exists at all (see NEXT_SESSION_PROMPT.md item 4).
 */
@Component({
  selector: 'app-admin-business-details',
  templateUrl: './admin-business-details.component.html',
  styleUrls: ['./admin-business-details.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    RouterLink,
    LocalizationPipe,
    PageHeaderComponent,
    StatusBadgeComponent,
    LoadingSpinnerComponent,
    ErrorStateComponent,
    EmptyStateComponent,
    PaginationComponent,
  ],
})
export class AdminBusinessDetailsComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly tenantsService = inject(AdminTenantsService);
  private readonly categoriesService = inject(CategoriesService);
  private readonly ticketsService = inject(SupportTicketsService);
  private readonly confirmation = inject(ConfirmationService);
  private readonly toaster = inject(ToasterService);
  private readonly permissionService = inject(PermissionService);

  protected readonly ApprovalStatus = TenantApprovalStatus;
  protected readonly TicketStatus = SupportTicketStatus;
  private readonly ticketsPageSize = 10;

  protected readonly tenant = signal<AdminTenantDto | null>(null);
  protected readonly isLoading = signal(true);
  protected readonly loadFailed = signal(false);
  protected readonly notFound = signal(false);
  protected readonly categoryName = signal<string | null>(null);
  protected readonly openTicketsCount = signal<number | null>(null);
  protected readonly activeTab = signal<DetailTab>('overview');

  protected readonly canApprove = computed(() => this.permissionService.getGrantedPolicy('Eksabli.Tenants.Approve'));
  protected readonly canSuspend = computed(() => this.permissionService.getGrantedPolicy('Eksabli.Tenants.Suspend'));
  protected readonly canViewBilling = computed(() => this.permissionService.getGrantedPolicy('Eksabli.Billing.ManagePlatform'));
  protected readonly canViewTickets = computed(() => this.permissionService.getGrantedPolicy('Eksabli.SupportTickets.Manage'));

  protected readonly tickets = signal<SupportTicketDto[]>([]);
  protected readonly ticketsTotalCount = signal(0);
  protected readonly ticketsLoading = signal(false);
  protected readonly ticketsFailed = signal(false);
  protected readonly ticketsLoaded = signal(false);
  protected readonly ticketsPageIndex = signal(0);
  protected readonly ticketsTotalPages = computed(() => Math.max(1, Math.ceil(this.ticketsTotalCount() / this.ticketsPageSize)));

  private tenantId: string | null = null;

  ngOnInit(): void {
    this.route.paramMap.subscribe((params) => {
      const tenantId = params.get('tenantId');
      if (!tenantId) return;
      this.tenantId = tenantId;
      this.activeTab.set('overview');
      this.ticketsLoaded.set(false);
      this.load(tenantId);
    });
  }

  protected retry(): void {
    if (this.tenantId) this.load(this.tenantId);
  }

  protected selectTab(tab: DetailTab): void {
    this.activeTab.set(tab);
    if (tab === 'tickets' && !this.ticketsLoaded() && this.tenantId) {
      this.loadTickets(this.tenantId);
    }
  }

  protected goToTicketsPage(index: number): void {
    if (index < 0 || index >= this.ticketsTotalPages() || !this.tenantId) return;
    this.ticketsPageIndex.set(index);
    this.loadTickets(this.tenantId);
  }

  protected retryTickets(): void {
    if (this.tenantId) this.loadTickets(this.tenantId);
  }

  protected statusLabelKey(status: TenantApprovalStatus | undefined): string {
    switch (status) {
      case TenantApprovalStatus.Approved:
        return '::AdminPanel:Businesses:StatusApproved';
      case TenantApprovalStatus.Suspended:
        return '::AdminPanel:Businesses:StatusSuspended';
      default:
        return '::AdminPanel:Businesses:StatusPending';
    }
  }

  protected statusVariant(status: TenantApprovalStatus | undefined): StatusBadgeVariant {
    switch (status) {
      case TenantApprovalStatus.Approved:
        return 'success';
      case TenantApprovalStatus.Suspended:
        return 'danger';
      default:
        return 'warning';
    }
  }

  protected ticketStatusLabelKey(status: SupportTicketStatus | undefined): string {
    switch (status) {
      case SupportTicketStatus.Open:
        return '::AdminPanel:SupportTickets:StatusOpen';
      case SupportTicketStatus.InProgress:
        return '::AdminPanel:SupportTickets:StatusInProgress';
      case SupportTicketStatus.Resolved:
        return '::AdminPanel:SupportTickets:StatusResolved';
      default:
        return '::AdminPanel:SupportTickets:StatusClosed';
    }
  }

  protected ticketStatusVariant(status: SupportTicketStatus | undefined): StatusBadgeVariant {
    switch (status) {
      case SupportTicketStatus.Open:
        return 'info';
      case SupportTicketStatus.InProgress:
        return 'warning';
      case SupportTicketStatus.Resolved:
        return 'success';
      default:
        return 'neutral';
    }
  }

  protected ticketPriorityLabelKey(priority: SupportTicketPriority | undefined): string {
    switch (priority) {
      case SupportTicketPriority.Low:
        return '::AdminPanel:SupportTickets:PriorityLow';
      case SupportTicketPriority.Medium:
        return '::AdminPanel:SupportTickets:PriorityMedium';
      case SupportTicketPriority.High:
        return '::AdminPanel:SupportTickets:PriorityHigh';
      default:
        return '::AdminPanel:SupportTickets:PriorityUrgent';
    }
  }

  protected ticketPriorityVariant(priority: SupportTicketPriority | undefined): StatusBadgeVariant {
    switch (priority) {
      case SupportTicketPriority.Low:
        return 'neutral';
      case SupportTicketPriority.Medium:
        return 'info';
      case SupportTicketPriority.High:
        return 'warning';
      default:
        return 'danger';
    }
  }

  protected approve(): void {
    const tenant = this.tenant();
    if (!tenant?.tenantId) return;
    this.confirmation
      .warn('::AdminPanel:Businesses:ApproveConfirmMessage', '::AdminPanel:Businesses:ApproveConfirmTitle')
      .subscribe((status) => {
        if (status !== Confirmation.Status.confirm || !tenant.tenantId) return;
        this.tenantsService.approve(tenant.tenantId).subscribe((updated) => {
          this.tenant.set(updated);
          this.toaster.success('::AdminPanel:Businesses:ApprovedMessage');
        });
      });
  }

  protected suspend(): void {
    const tenant = this.tenant();
    if (!tenant?.tenantId) return;
    this.confirmation
      .warn('::AdminPanel:Businesses:SuspendConfirmMessage', '::AdminPanel:Businesses:SuspendConfirmTitle')
      .subscribe((status) => {
        if (status !== Confirmation.Status.confirm || !tenant.tenantId) return;
        this.tenantsService.suspend(tenant.tenantId).subscribe((updated) => {
          this.tenant.set(updated);
          this.toaster.success('::AdminPanel:Businesses:SuspendedMessage');
        });
      });
  }

  private load(tenantId: string): void {
    this.isLoading.set(true);
    this.loadFailed.set(false);
    this.notFound.set(false);
    this.categoryName.set(null);
    this.openTicketsCount.set(null);

    this.tenantsService.get(tenantId).subscribe({
      next: (tenant) => {
        this.tenant.set(tenant);
        this.isLoading.set(false);
        if (tenant.categoryId) this.loadCategoryName(tenant.categoryId);
        if (this.canViewTickets()) this.loadOpenTicketsCount(tenantId);
      },
      error: (err: HttpErrorResponse) => {
        this.isLoading.set(false);
        if (err.status === 404) {
          this.notFound.set(true);
        } else {
          this.loadFailed.set(true);
        }
      },
    });
  }

  private loadCategoryName(categoryId: string): void {
    this.categoriesService.get(categoryId).subscribe({
      next: (category) => this.categoryName.set(category.nameEn ?? null),
      // Best-effort — falls back to the em dash in the template rather than blocking the page.
      error: () => undefined,
    });
  }

  private loadOpenTicketsCount(tenantId: string): void {
    this.ticketsService
      .getList({ tenantId, status: SupportTicketStatus.Open, priority: null, sorting: undefined, skipCount: 0, maxResultCount: 0 })
      .subscribe({
        next: (result) => this.openTicketsCount.set(result.totalCount ?? 0),
        // Supplementary stat — stays null (hidden in the template) rather than showing an error.
        error: () => undefined,
      });
  }

  private loadTickets(tenantId: string): void {
    this.ticketsLoading.set(true);
    this.ticketsFailed.set(false);
    this.ticketsService
      .getList({
        tenantId,
        status: null,
        priority: null,
        sorting: 'lastModificationTime desc',
        skipCount: this.ticketsPageIndex() * this.ticketsPageSize,
        maxResultCount: this.ticketsPageSize,
      })
      .subscribe({
        next: (result) => {
          this.tickets.set(result.items ?? []);
          this.ticketsTotalCount.set(result.totalCount ?? 0);
          this.ticketsLoading.set(false);
          this.ticketsLoaded.set(true);
        },
        error: () => {
          this.ticketsLoading.set(false);
          this.ticketsFailed.set(true);
        },
      });
  }
}
