import { DatePipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { LocalizationPipe, PermissionService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { AdminSubscriptionsService } from '../../proxy/controllers/admin-subscriptions.service';
import { AdminTenantsService } from '../../proxy/controllers/admin-tenants.service';
import type { InvoiceDto, PaymentDto, TenantSubscriptionDto } from '../../proxy/billing/models';
import { TenantSubscriptionStatus } from '../../proxy/billing/tenant-subscription-status.enum';
import { InvoiceStatus } from '../../proxy/billing/invoice-status.enum';
import { PaymentStatus } from '../../proxy/billing/payment-status.enum';
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
 * Remaining real gap, documented rather than faked:
 * - `[MISSING BACKEND CAPABILITY]` No search — `AdminSubscriptionFilterDto` only has `status`, no
 *   `filterText`. No search box on this page.
 *
 * "Subscription Details" is not a routed page — per the backend readiness doc's recommendation
 * (§3.6), it's an expandable row showing that subscription's invoices (`GetInvoicesAsync` supports
 * filtering by `tenantSubscriptionId`), avoiding a missing-GetAsync endpoint and a page that would
 * break on refresh/deep-link. Paid invoices nest one level further: `GetPaymentsAsync` (added to close
 * the gap where `RecordManualPaymentAsync` wrote `Payment` rows with no read path) is called filtered
 * by `invoiceId` when a Paid invoice's own row is expanded.
 *
 * Stats (`loadStats`) call a real, dedicated `GetStatsAsync` endpoint added specifically for this page —
 * previously this fired two extra concurrent `GetListAsync` calls against `/api/app/admin-subscriptions`
 * (status=Active with up to 500 rows transferred just to sum MRR client-side, plus a separate
 * status=Trialing count-only call) on top of the paginated list's own call to the same endpoint — three
 * simultaneous requests to one endpoint on every page load, which is what surfaced as a benign-but-noisy
 * `OperationCanceledException` in the server log if the page was left before they all completed. One
 * server-side call now (DB-level GroupBy over active subscriptions, joined against plan prices — see
 * AdminSubscriptionAppService.GetStatsAsync) — one round trip, and the true total MRR, not a
 * first-500-rows approximation.
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
  private readonly toaster = inject(ToasterService);
  private readonly permissionService = inject(PermissionService);

  protected readonly SubStatus = TenantSubscriptionStatus;
  protected readonly InvStatus = InvoiceStatus;
  protected readonly PayStatus = PaymentStatus;
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

  /** Supplementary stat row — all three numbers come from one real endpoint (`GetStatsAsync`), never
   *  invented. "Approx." on the MRR label stays even though it's now a true total, not a capped
   *  approximation: the API exposes list price only, no visibility into discounts/proration, so it's
   *  still an estimate of actual billed revenue, just no longer additionally capped at 500 rows.
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

  protected readonly expandedInvoiceId = signal<string | null>(null);
  protected readonly payments = signal<PaymentDto[]>([]);
  protected readonly paymentsLoading = signal(false);
  protected readonly paymentsFailed = signal(false);

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
    this.expandedInvoiceId.set(null);
    if (this.expandedId() === id) {
      this.expandedId.set(null);
      return;
    }
    this.expandedId.set(id);
    this.loadInvoices(id);
  }

  protected toggleInvoiceExpand(invoice: InvoiceDto): void {
    const id = invoice.id;
    if (!id) return;
    if (this.expandedInvoiceId() === id) {
      this.expandedInvoiceId.set(null);
      return;
    }
    this.expandedInvoiceId.set(id);
    this.loadPayments(id);
  }

  protected retryPayments(invoice: InvoiceDto): void {
    if (!invoice.id) return;
    this.loadPayments(invoice.id);
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
          this.expandedInvoiceId.set(null);
          const expanded = this.expandedId();
          if (expanded) this.loadInvoices(expanded);
        },
        error: () => {
          this.isSavingPayment.set(false);
          this.toaster.error('::AdminPanel:Subscriptions:PaymentErrorMessage');
        },
      });
  }

  protected paymentStatusLabelKey(status: PaymentStatus | undefined): string {
    switch (status) {
      case PaymentStatus.Succeeded:
        return '::AdminPanel:Subscriptions:PaymentSucceeded';
      case PaymentStatus.Failed:
        return '::AdminPanel:Subscriptions:PaymentFailed';
      default:
        return '::AdminPanel:Subscriptions:PaymentPending';
    }
  }

  protected paymentStatusVariant(status: PaymentStatus | undefined): StatusBadgeVariant {
    switch (status) {
      case PaymentStatus.Succeeded:
        return 'success';
      case PaymentStatus.Failed:
        return 'danger';
      default:
        return 'info';
    }
  }

  private loadPayments(invoiceId: string): void {
    this.paymentsLoading.set(true);
    this.paymentsFailed.set(false);
    this.subscriptionsService
      .getPayments({ invoiceId, status: null, sorting: 'creationTime desc', skipCount: 0, maxResultCount: 50 })
      .subscribe({
        next: (result) => {
          // Ignore a slow response for an invoice the admin has since collapsed/switched away from —
          // without this check, expanding A then quickly expanding B before A's request resolves can
          // let A's payments land under B's now-expanded row once A's response finally arrives.
          if (this.expandedInvoiceId() !== invoiceId) return;
          this.payments.set(result.items ?? []);
          this.paymentsLoading.set(false);
        },
        error: () => {
          if (this.expandedInvoiceId() !== invoiceId) return;
          this.paymentsLoading.set(false);
          this.paymentsFailed.set(true);
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
    this.subscriptionsService.getStats().subscribe({
      next: (stats) => {
        this.activeCount.set(stats.activeCount);
        this.trialingCount.set(stats.trialingCount);
        this.approxMrr.set(stats.approxMrr);
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
