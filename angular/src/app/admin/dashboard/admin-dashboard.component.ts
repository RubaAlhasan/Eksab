import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { LocalizationPipe, PermissionService } from '@abp/ng.core';
import { AdminTenantsService } from '../../proxy/controllers/admin-tenants.service';
import { AdminSubscriptionsService } from '../../proxy/controllers/admin-subscriptions.service';
import { SupportTicketsService } from '../../proxy/controllers/support-tickets.service';
import { CategoriesService } from '../../proxy/controllers/categories.service';
import type { AdminTenantDto } from '../../proxy/businesses/models';
import type { CategoryDto, SupportTicketDto } from '../../proxy/platform/models';
import { TenantApprovalStatus } from '../../proxy/business-profiles/tenant-approval-status.enum';
import { SupportTicketStatus } from '../../proxy/platform/support-ticket-status.enum';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { StatusBadgeComponent, StatusBadgeVariant } from '../../shared/components/status-badge/status-badge.component';
import { LoadingSpinnerComponent } from '../../shared/components/loading-spinner/loading-spinner.component';
import { ErrorStateComponent } from '../../shared/components/error-state/error-state.component';

/**
 * Admin Portal > Dashboard — the last Admin Portal MVP page, deliberately built last (composes widgets
 * from pages that all already exist: Businesses, Categories, Subscriptions, Support Tickets). Mirrors
 * prototype/admin/dashboard.html's layout, but every widget is checked against a real endpoint first —
 * two of the prototype's four stat tiles and its entire MRR trend chart are `[MISSING BACKEND
 * CAPABILITY]` and deliberately NOT built:
 * - **Daily Active Users** — no login/session tracking exists anywhere in this codebase (same gap
 *   already documented for Members' "Last Active" column, which falls back to wallet activity
 *   instead). No real number to show; omitted entirely, not faked.
 * - **MRR trend chart / "+X% (7mo)" badge** — `AdminSubscriptionAppService.GetStatsAsync()` (added
 *   earlier this session) gives one accurate CURRENT MRR figure, DB-computed — real and shown as a
 *   stat tile — but there's no historical/time-series snapshot table anywhere to chart a trend against
 *   or compute a real period-over-period delta from. No chart, no growth badge.
 *
 * What IS real and shown:
 * - **Total Businesses** / **Pending Approvals** (count + list) — `AdminTenantsService.getList` (one
 *   call, `approvalStatus: null, maxResultCount: 500, sorting: 'creationTime desc'` — same "acceptable
 *   at this scale" bounded-batch assumption `admin-tenants.component.ts` already uses for its own bulk
 *   Category/Plan lookups). `totalCount` from that response is the exact, DB-computed grand total
 *   regardless of the 500 cap; Pending Approvals count/list are computed client-side from the same
 *   batch — accurate as long as total businesses stays under ~500, the same assumption already made
 *   elsewhere in this app. Serves THREE needs from one request (total count, pending list, tenant-name
 *   lookup for the Recent Tickets widget below) specifically to avoid re-introducing the "several
 *   concurrent requests to the same list endpoint" issue fixed earlier this session on the Subscriptions
 *   page — see admin-subscriptions.component.ts's own history.
 * - **Platform MRR** — `AdminSubscriptionsService.getStats()`, gated on `Eksabli.Billing.ManagePlatform`
 *   (tile hidden entirely, call skipped, for a viewer without it — same pattern as the Businesses page's
 *   Plan column).
 * - **Open Support Tickets** (count) / **Recent Support Tickets** (list) — `SupportTicketsService
 *   .getList`, twice (once `status: Open, maxResultCount: 1` for the accurate count — reads
 *   `totalCount`, ignores the single item; `1` is the lowest value ABP's own `LimitedResultRequestDto
 *   .MaxResultCount` accepts, `[Range(1, ...)]` rejects `0` with a 400 — caught live and fixed here and
 *   in admin-business-details.component.ts, which had the same bug — once unfiltered
 *   `sorting: lastModificationTime desc, maxResultCount: 4` for the preview list) — same two-calls-one-
 *   endpoint shape `admin-business-details.component.ts`'s Overview tab already established as fine
 *   (the earlier Subscriptions issue was about redundant/overlapping fetches for client-side math, not
 *   merely "more than one call to an endpoint"). Both gated on `Eksabli.SupportTickets.Manage` — that
 *   controller has no lesser read, whole widget area hidden without it.
 * - **Category Mix** — `CategoriesService.getList` ([AllowAnonymous], already used elsewhere), top 5 by
 *   the real `CategoryDto.BusinessCount` field (added earlier this session), bars scaled relative to
 *   the largest visible category — not the prototype's own arbitrary "× 3 of the total" fudge factor.
 */
@Component({
  selector: 'app-admin-dashboard',
  templateUrl: './admin-dashboard.component.html',
  styleUrls: ['./admin-dashboard.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DecimalPipe,
    RouterLink,
    LocalizationPipe,
    PageHeaderComponent,
    StatusBadgeComponent,
    LoadingSpinnerComponent,
    ErrorStateComponent,
  ],
})
export class AdminDashboardComponent implements OnInit {
  private readonly tenantsService = inject(AdminTenantsService);
  private readonly subscriptionsService = inject(AdminSubscriptionsService);
  private readonly ticketsService = inject(SupportTicketsService);
  private readonly categoriesService = inject(CategoriesService);
  private readonly permissionService = inject(PermissionService);

  protected readonly canViewMrr = computed(() => this.permissionService.getGrantedPolicy('Eksabli.Billing.ManagePlatform'));
  protected readonly canViewTickets = computed(() => this.permissionService.getGrantedPolicy('Eksabli.SupportTickets.Manage'));

  protected readonly businessesLoading = signal(true);
  protected readonly businessesFailed = signal(false);
  protected readonly totalBusinesses = signal<number | null>(null);
  protected readonly pendingApprovals = signal<AdminTenantDto[]>([]);
  protected readonly pendingCount = signal<number | null>(null);
  private readonly tenantNameById = signal<Map<string, string>>(new Map());

  protected readonly mrrLoading = signal(false);
  protected readonly mrr = signal<number | null>(null);

  protected readonly ticketsLoading = signal(false);
  protected readonly ticketsFailed = signal(false);
  protected readonly openTicketsCount = signal<number | null>(null);
  protected readonly recentTickets = signal<SupportTicketDto[]>([]);

  protected readonly categoriesLoading = signal(true);
  private readonly categories = signal<CategoryDto[]>([]);
  private readonly categoryNameById = signal<Map<string, string>>(new Map());
  protected readonly topCategories = computed(() =>
    [...this.categories()].sort((a, b) => b.businessCount - a.businessCount).slice(0, 5),
  );
  protected readonly categoryMixMax = computed(() => Math.max(1, ...this.topCategories().map((c) => c.businessCount)));

  ngOnInit(): void {
    this.loadBusinesses();
    this.loadCategories();
    if (this.canViewMrr()) this.loadMrr();
    if (this.canViewTickets()) this.loadTickets();
  }

  protected initials(name: string | undefined): string {
    if (!name) return '?';
    return name
      .split(' ')
      .filter(Boolean)
      .map((word) => word[0])
      .join('')
      .slice(0, 2)
      .toUpperCase();
  }

  protected categoryName(categoryId: string | null | undefined): string {
    if (!categoryId) return '—';
    return this.categoryNameById().get(categoryId) ?? '—';
  }

  protected statusLabelKey(status: TenantApprovalStatus | undefined): string {
    return status === TenantApprovalStatus.Suspended
      ? '::AdminPanel:Businesses:StatusSuspended'
      : '::AdminPanel:Businesses:StatusPending';
  }

  protected statusVariant(status: TenantApprovalStatus | undefined): StatusBadgeVariant {
    return status === TenantApprovalStatus.Suspended ? 'danger' : 'warning';
  }

  protected ticketFrom(ticket: SupportTicketDto): string {
    if (ticket.tenantId) return this.tenantNameById().get(ticket.tenantId) ?? ticket.tenantId;
    return '';
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
        return 'danger';
      case SupportTicketStatus.InProgress:
        return 'warning';
      case SupportTicketStatus.Resolved:
        return 'success';
      default:
        return 'neutral';
    }
  }

  protected retryBusinesses(): void {
    this.loadBusinesses();
  }

  protected retryTickets(): void {
    this.loadTickets();
  }

  private loadBusinesses(): void {
    this.businessesLoading.set(true);
    this.businessesFailed.set(false);
    this.tenantsService
      .getList({ filterText: null, approvalStatus: null, sorting: 'creationTime desc', skipCount: 0, maxResultCount: 500 })
      .subscribe({
        next: (result) => {
          const items = result.items ?? [];
          const pending = items.filter((t) => t.approvalStatus === TenantApprovalStatus.Pending);
          this.totalBusinesses.set(result.totalCount ?? items.length);
          this.pendingCount.set(pending.length);
          this.pendingApprovals.set(pending.slice(0, 5));

          const nameMap = new Map<string, string>();
          for (const tenant of items) {
            if (tenant.tenantId) nameMap.set(tenant.tenantId, tenant.tenantName ?? tenant.tenantId);
          }
          this.tenantNameById.set(nameMap);

          this.businessesLoading.set(false);
        },
        error: () => {
          this.businessesLoading.set(false);
          this.businessesFailed.set(true);
        },
      });
  }

  private loadMrr(): void {
    this.mrrLoading.set(true);
    this.subscriptionsService.getStats().subscribe({
      next: (stats) => {
        this.mrr.set(stats.approxMrr);
        this.mrrLoading.set(false);
      },
      // Best-effort — the tile just stays hidden (see the template's `mrr(); as value` guard),
      // doesn't block the rest of the dashboard.
      error: () => this.mrrLoading.set(false),
    });
  }

  private loadTickets(): void {
    this.ticketsLoading.set(true);
    this.ticketsFailed.set(false);

    this.ticketsService
      .getList({ status: SupportTicketStatus.Open, priority: null, tenantId: null, sorting: undefined, skipCount: 0, maxResultCount: 1 })
      .subscribe({
        next: (result) => this.openTicketsCount.set(result.totalCount ?? 0),
        error: () => undefined,
      });

    this.ticketsService
      .getList({ status: null, priority: null, tenantId: null, sorting: 'lastModificationTime desc', skipCount: 0, maxResultCount: 4 })
      .subscribe({
        next: (result) => {
          this.recentTickets.set(result.items ?? []);
          this.ticketsLoading.set(false);
        },
        error: () => {
          this.ticketsLoading.set(false);
          this.ticketsFailed.set(true);
        },
      });
  }

  private loadCategories(): void {
    this.categoriesLoading.set(true);
    this.categoriesService.getList({ parentCategoryId: null, filterText: null, skipCount: 0, maxResultCount: 500 }).subscribe({
      next: (result) => {
        const items = result.items ?? [];
        this.categories.set(items);
        const nameMap = new Map<string, string>();
        for (const category of items) {
          if (category.id) nameMap.set(category.id, category.nameEn ?? category.id);
        }
        this.categoryNameById.set(nameMap);
        this.categoriesLoading.set(false);
      },
      // Best-effort — Category Mix card just stays empty, doesn't block the rest of the dashboard.
      error: () => this.categoriesLoading.set(false),
    });
  }
}
