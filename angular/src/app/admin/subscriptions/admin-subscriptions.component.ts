import { DatePipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { forkJoin } from 'rxjs';
import { LocalizationPipe, PermissionService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { AdminSubscriptionsService } from '../../proxy/controllers/admin-subscriptions.service';
import { AdminTenantsService } from '../../proxy/controllers/admin-tenants.service';
import { SubscriptionPlansService } from '../../proxy/controllers/subscription-plans.service';
import type { InvoiceDto, TenantSubscriptionDto } from '../../proxy/billing/models';
import { TenantSubscriptionStatus } from '../../proxy/billing/tenant-subscription-status.enum';
import { InvoiceStatus } from '../../proxy/billing/invoice-status.enum';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { LoadingSpinnerComponent } from '../../shared/components/loading-spinner/loading-spinner.component';
import { ErrorStateComponent } from '../../shared/components/error-state/error-state.component';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { PaginationComponent } from '../../shared/components/pagination/pagination.component';
import { StatusBadgeComponent, StatusBadgeVariant } from '../../shared/components/status-badge/status-badge.component';
import { ModalComponent } from '../../shared/components/modal/modal.component';

/**
 * Admin Portal > Subscriptions — every tenant's subscription, platform-wide. Wired against the real
 * `AdminSubscriptionsController`/`AdminSubscriptionAppService`, both confirmed (by reading the actual
 * service body, not just the controller) to correctly wrap every query in
 * `_dataFilter.Disable<IMultiTenant>()` — see admin-portal-backend-readiness.md §5.
 *
 * Two real gaps, both documented rather than faked:
 * - `[MISSING BACKEND CAPABILITY]` No search — `AdminSubscriptionFilterDto` only has `status`, no
 *   `filterText`. No search box on this page.
 * - `[MISSING BACKEND CAPABILITY]` No `GetAsync(id)` for a single subscription — per the backend
 *   readiness doc's own recommendation (§3.6), "Subscription Details" is not a routed page here; it's
 *   an expandable row showing that subscription's invoices (`GetInvoicesAsync` DOES support filtering
 *   by `tenantSubscriptionId`, which is real), avoiding both the missing endpoint and a page that would
 *   break on refresh/deep-link.
 */
@Component({
  selector: 'app-admin-subscriptions',
  templateUrl: './admin-subscriptions.component.html',
  styleUrls: ['./admin-subscriptions.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    DecimalPipe,
    ReactiveFormsModule,
    LocalizationPipe,
    PageHeaderComponent,
    LoadingSpinnerComponent,
    ErrorStateComponent,
    EmptyStateComponent,
    PaginationComponent,
    StatusBadgeComponent,
    ModalComponent,
  ],
})
export class AdminSubscriptionsComponent implements OnInit {
  private readonly subscriptionsService = inject(AdminSubscriptionsService);
  private readonly tenantsService = inject(AdminTenantsService);
  private readonly plansService = inject(SubscriptionPlansService);
  private readonly toaster = inject(ToasterService);
  private readonly permissionService = inject(PermissionService);

  protected readonly SubStatus = TenantSubscriptionStatus;
  protected readonly InvStatus = InvoiceStatus;
  private readonly pageSize = 10;

  protected readonly subscriptions = signal<TenantSubscriptionDto[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly isLoading = signal(true);
  protected readonly loadFailed = signal(false);
  protected readonly statusFilterValue = signal('');
  protected readonly pageIndex = signal(0);
  protected readonly totalPages = computed(() => Math.max(1, Math.ceil(this.totalCount() / this.pageSize)));

  /** Resolves a bare `tenantId` GUID to a display name — `TenantSubscriptionDto` carries no name of
   *  its own. Loaded once, not per-row/per-page: same "load a bounded batch into memory" scale
   *  assumption `AdminTenantAppService` itself already makes server-side (it loads every
   *  `BusinessProfile` and paginates in C#, not in SQL) — matching the existing codebase's own
   *  approach rather than inventing a stricter one. Revisit if the tenant count genuinely grows past
   *  this batch size. */
  private readonly tenantNames = signal<Map<string, string>>(new Map());

  protected readonly canManage = computed(() => this.permissionService.getGrantedPolicy('Eksabli.Billing.ManagePlatform'));

  /** Supplementary stat row — all three numbers come from real endpoints, never invented:
   *  - Active/Trialing counts: `totalCount` from a `maxResultCount: 0` filtered query (server counts,
   *    no items transferred).
   *  - Approx. MRR: real `monthlyPrice` per plan × count of active subscriptions on that plan, summed
   *    client-side. Labelled "approx." because the API exposes list price only — no visibility into
   *    discounts/proration, and (matching the same batch-size assumption used elsewhere on this page
   *    for tenant names) it sums at most the first 500 active subscriptions.
   *  Deliberately NOT shown: a "Churn" stat — there is no time-series/analytics endpoint to compute it
   *  from, so it would have to be fabricated. See admin-subscriptions.component.ts's file-level
   *  comment for the [MISSING BACKEND CAPABILITY] list. Stats fail silently (stay null) rather than
   *  blocking the page — they're supplementary, not the primary content. */
  protected readonly statsLoading = signal(true);
  protected readonly activeCount = signal<number | null>(null);
  protected readonly trialingCount = signal<number | null>(null);
  protected readonly approxMrr = signal<number | null>(null);

  protected readonly expandedId = signal<string | null>(null);
  protected readonly invoices = signal<InvoiceDto[]>([]);
  protected readonly invoicesLoading = signal(false);
  protected readonly invoicesFailed = signal(false);

  protected readonly paymentModalOpen = signal(false);
  protected readonly isSavingPayment = signal(false);
  private payingInvoiceId: string | null = null;

  protected readonly paymentForm = new FormGroup({
    providerTransactionRef: new FormControl('', { nonNullable: true }),
  });

  ngOnInit(): void {
    this.loadTenantNames();
    this.loadStats();
    this.load();
  }

  protected tenantName(tenantId: string | null | undefined): string {
    if (!tenantId) return '—';
    return this.tenantNames().get(tenantId) ?? tenantId;
  }

  protected onStatusFilterChange(event: Event): void {
    this.statusFilterValue.set((event.target as HTMLSelectElement).value);
    this.pageIndex.set(0);
    this.load();
  }

  protected goToPage(index: number): void {
    if (index < 0 || index >= this.totalPages()) return;
    this.pageIndex.set(index);
    this.load();
  }

  protected retry(): void {
    this.load();
  }

  protected subStatusLabelKey(status: TenantSubscriptionStatus | undefined): string {
    switch (status) {
      case TenantSubscriptionStatus.Active:
        return '::AdminPanel:Subscriptions:StatusActive';
      case TenantSubscriptionStatus.PastDue:
        return '::AdminPanel:Subscriptions:StatusPastDue';
      case TenantSubscriptionStatus.Cancelled:
        return '::AdminPanel:Subscriptions:StatusCancelled';
      default:
        return '::AdminPanel:Subscriptions:StatusTrialing';
    }
  }

  protected subStatusVariant(status: TenantSubscriptionStatus | undefined): StatusBadgeVariant {
    switch (status) {
      case TenantSubscriptionStatus.Active:
        return 'success';
      case TenantSubscriptionStatus.PastDue:
        return 'danger';
      case TenantSubscriptionStatus.Cancelled:
        return 'neutral';
      default:
        return 'info';
    }
  }

  protected invStatusLabelKey(status: InvoiceStatus | undefined): string {
    switch (status) {
      case InvoiceStatus.Paid:
        return '::AdminPanel:Subscriptions:InvoicePaid';
      case InvoiceStatus.Overdue:
        return '::AdminPanel:Subscriptions:InvoiceOverdue';
      case InvoiceStatus.Sent:
        return '::AdminPanel:Subscriptions:InvoiceSent';
      default:
        return '::AdminPanel:Subscriptions:InvoiceDraft';
    }
  }

  protected invStatusVariant(status: InvoiceStatus | undefined): StatusBadgeVariant {
    switch (status) {
      case InvoiceStatus.Paid:
        return 'success';
      case InvoiceStatus.Overdue:
        return 'danger';
      case InvoiceStatus.Sent:
        return 'warning';
      default:
        return 'neutral';
    }
  }

  protected retryInvoices(subscription: TenantSubscriptionDto): void {
    if (!subscription.id) return;
    this.loadInvoices(subscription.id);
  }

  protected toggleExpand(subscription: TenantSubscriptionDto): void {
    const id = subscription.id;
    if (!id) return;
    if (this.expandedId() === id) {
      this.expandedId.set(null);
      return;
    }
    this.expandedId.set(id);
    this.loadInvoices(id);
  }

  protected openRecordPaymentModal(invoice: InvoiceDto): void {
    if (!invoice.id) return;
    this.payingInvoiceId = invoice.id;
    this.paymentForm.reset({ providerTransactionRef: '' });
    this.paymentModalOpen.set(true);
  }

  protected closePaymentModal(): void {
    this.paymentModalOpen.set(false);
  }

  protected submitPayment(): void {
    if (!this.payingInvoiceId) return;
    const providerTransactionRef = this.paymentForm.getRawValue().providerTransactionRef || null;

    this.isSavingPayment.set(true);
    this.subscriptionsService
      .recordManualPayment({ invoiceId: this.payingInvoiceId, providerTransactionRef })
      .subscribe({
        next: () => {
          this.isSavingPayment.set(false);
          this.paymentModalOpen.set(false);
          this.toaster.success('::AdminPanel:Subscriptions:PaymentRecordedMessage');
          const expanded = this.expandedId();
          if (expanded) this.loadInvoices(expanded);
        },
        error: () => {
          this.isSavingPayment.set(false);
          this.toaster.error('::AdminPanel:Subscriptions:PaymentErrorMessage');
        },
      });
  }

  private loadInvoices(tenantSubscriptionId: string): void {
    this.invoicesLoading.set(true);
    this.invoicesFailed.set(false);
    this.subscriptionsService
      .getInvoices({ tenantSubscriptionId, status: null, sorting: 'dueDate desc', skipCount: 0, maxResultCount: 50 })
      .subscribe({
        next: (result) => {
          this.invoices.set(result.items ?? []);
          this.invoicesLoading.set(false);
        },
        error: () => {
          this.invoicesLoading.set(false);
          this.invoicesFailed.set(true);
        },
      });
  }

  private loadStats(): void {
    this.statsLoading.set(true);
    forkJoin({
      plans: this.plansService.getList({ maxResultCount: 1000 }),
      active: this.subscriptionsService.getList({
        status: TenantSubscriptionStatus.Active,
        sorting: undefined,
        skipCount: 0,
        maxResultCount: 500,
      }),
      trialing: this.subscriptionsService.getList({
        status: TenantSubscriptionStatus.Trialing,
        sorting: undefined,
        skipCount: 0,
        maxResultCount: 0,
      }),
    }).subscribe({
      next: ({ plans, active, trialing }) => {
        const priceByPlanId = new Map<string, number>();
        for (const plan of plans.items ?? []) {
          if (plan.id) priceByPlanId.set(plan.id, plan.monthlyPrice ?? 0);
        }
        const mrr = (active.items ?? []).reduce(
          (sum, sub) => sum + (sub.planId ? (priceByPlanId.get(sub.planId) ?? 0) : 0),
          0,
        );
        this.activeCount.set(active.totalCount ?? 0);
        this.trialingCount.set(trialing.totalCount ?? 0);
        this.approxMrr.set(mrr);
        this.statsLoading.set(false);
      },
      // Stats are supplementary — leave them null (hidden in the template) rather than showing an
      // error state that would compete with the actual subscriptions table below.
      error: () => this.statsLoading.set(false),
    });
  }

  private loadTenantNames(): void {
    this.tenantsService
      .getList({ filterText: null, approvalStatus: null, skipCount: 0, maxResultCount: 500 })
      .subscribe({
        next: (result) => {
          const map = new Map<string, string>();
          for (const tenant of result.items ?? []) {
            if (tenant.tenantId) map.set(tenant.tenantId, tenant.tenantName ?? tenant.tenantId);
          }
          this.tenantNames.set(map);
        },
        // Name resolution failing shouldn't block the subscriptions list itself — rows just fall back
        // to showing the raw tenantId (see `tenantName()` above).
        error: () => undefined,
      });
  }

  private load(): void {
    this.isLoading.set(true);
    this.loadFailed.set(false);
    this.expandedId.set(null);

    const status =
      this.statusFilterValue() === '' ? null : (Number(this.statusFilterValue()) as TenantSubscriptionStatus);

    this.subscriptionsService
      .getList({
        status,
        sorting: 'renewalDate asc',
        skipCount: this.pageIndex() * this.pageSize,
        maxResultCount: this.pageSize,
      })
      .subscribe({
        next: (result) => {
          this.subscriptions.set(result.items ?? []);
          this.totalCount.set(result.totalCount ?? 0);
          this.isLoading.set(false);
        },
        error: () => {
          this.isLoading.set(false);
          this.loadFailed.set(true);
        },
      });
  }
}
