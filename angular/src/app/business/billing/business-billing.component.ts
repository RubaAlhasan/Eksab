import { DatePipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { LocalizationPipe } from '@abp/ng.core';
import { BillingService } from '../../proxy/controllers/billing.service';
import type { InvoiceDto } from '../../proxy/billing/models';
import { InvoiceStatus } from '../../proxy/billing/invoice-status.enum';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { LoadingSpinnerComponent } from '../../shared/components/loading-spinner/loading-spinner.component';
import { ErrorStateComponent } from '../../shared/components/error-state/error-state.component';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { PaginationComponent } from '../../shared/components/pagination/pagination.component';
import { StatusBadgeComponent, StatusBadgeVariant } from '../../shared/components/status-badge/status-badge.component';

/**
 * Business Portal > Billing — mirrors prototype/business/billing.html, built against the real
 * `IBillingAppService.GetMyInvoicesAsync` (`Eksabli.Billing.ManageOwn`, same permission Subscription's
 * page and Branches' quota banner already use). No backend changes needed; the proxy already existed.
 *
 * Real, but a much smaller page than the prototype's own shape — two things the prototype shows have
 * no real backend behind them at all, confirmed by reading the whole `Eksabli.Billing` namespace:
 * - **`[MISSING BACKEND CAPABILITY]` — Payment Method card, dropped entirely.** No card/payment-method
 *   storage exists anywhere in the domain (`TenantSubscription`/`Invoice` carry no such field) — this
 *   app has no payment-gateway integration at all; `RecordManualPaymentAsync` (Host-admin only) is the
 *   only "payment" concept anywhere, and it's a manual bookkeeping entry, not a stored card.
 * - **`[MISSING BACKEND CAPABILITY]` — "Export All" / per-invoice "Download", dropped entirely.** No
 *   invoice PDF/export endpoint exists anywhere (unlike the real two-step Excel-export pattern
 *   Coupons/Dashboard use elsewhere in this app) — there is nothing to generate or download.
 * - **Real, shown**: "Next Invoice" tile — computed client-side as the earliest-due invoice that isn't
 *   `Paid` (from the same real list, sorted by `dueDate asc`), not a separate endpoint; a real derived
 *   figure, not fabricated. Falls back to "No upcoming invoice" if every invoice is `Paid`.
 * - **Invoice History**: real, paged, via `GetMyInvoicesAsync`. Status shows all 4 real `InvoiceStatus`
 *   values (`Draft`/`Sent`/`Paid`/`Overdue`), not the prototype's own 3 (`Paid`/`Due`/`Overdue`) —
 *   `Draft` is a real state (an invoice generated but not yet sent) the prototype doesn't account for.
 *   "Period" column shows the real `DueDate`'s month/year (`InvoiceDto` has no separate period label —
 *   the due date IS the billing period marker here) rather than inventing a "period" string.
 */
@Component({
  selector: 'app-business-billing',
  templateUrl: './business-billing.component.html',
  styleUrls: ['./business-billing.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, DecimalPipe, LocalizationPipe, PageHeaderComponent, LoadingSpinnerComponent, ErrorStateComponent, EmptyStateComponent, PaginationComponent, StatusBadgeComponent],
})
export class BusinessBillingComponent implements OnInit {
  private readonly billingService = inject(BillingService);

  protected readonly Status = InvoiceStatus;
  private readonly pageSize = 10;

  protected readonly invoices = signal<InvoiceDto[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly isLoading = signal(true);
  protected readonly loadFailed = signal(false);
  protected readonly pageIndex = signal(0);
  protected readonly totalPages = computed(() => Math.max(1, Math.ceil(this.totalCount() / this.pageSize)));

  protected readonly nextInvoice = signal<InvoiceDto | null>(null);
  protected readonly nextInvoiceLoading = signal(true);

  ngOnInit(): void {
    this.load();
    this.loadNextInvoice();
  }

  protected retry(): void {
    this.load();
  }

  protected statusLabelKey(status: InvoiceStatus | undefined): string {
    switch (status) {
      case InvoiceStatus.Paid:
        return '::BusinessPanel:Billing:StatusPaid';
      case InvoiceStatus.Overdue:
        return '::BusinessPanel:Billing:StatusOverdue';
      case InvoiceStatus.Sent:
        return '::BusinessPanel:Billing:StatusSent';
      default:
        return '::BusinessPanel:Billing:StatusDraft';
    }
  }

  protected statusVariant(status: InvoiceStatus | undefined): StatusBadgeVariant {
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

  protected goToPage(index: number): void {
    if (index < 0 || index >= this.totalPages()) return;
    this.pageIndex.set(index);
    this.load();
  }

  private load(): void {
    this.isLoading.set(true);
    this.loadFailed.set(false);
    this.billingService.getMyInvoices({ sorting: 'dueDate desc', skipCount: this.pageIndex() * this.pageSize, maxResultCount: this.pageSize }).subscribe({
      next: (result) => {
        this.invoices.set(result.items ?? []);
        this.totalCount.set(result.totalCount ?? 0);
        this.isLoading.set(false);
      },
      error: () => {
        this.isLoading.set(false);
        this.loadFailed.set(true);
      },
    });
  }

  /** Fetches a small bounded batch sorted by due date, picks the earliest non-`Paid` invoice
   *  client-side — no dedicated "next invoice" endpoint exists, but this is a real derivation from the
   *  real list, not a fabricated figure. Independent of the paged table's own `load()` above so
   *  paging the history doesn't disturb this tile. */
  private loadNextInvoice(): void {
    this.nextInvoiceLoading.set(true);
    this.billingService.getMyInvoices({ sorting: 'dueDate asc', skipCount: 0, maxResultCount: 20 }).subscribe({
      next: (result) => {
        const upcoming = (result.items ?? []).find((invoice) => invoice.status !== InvoiceStatus.Paid);
        this.nextInvoice.set(upcoming ?? null);
        this.nextInvoiceLoading.set(false);
      },
      error: () => this.nextInvoiceLoading.set(false),
    });
  }
}
