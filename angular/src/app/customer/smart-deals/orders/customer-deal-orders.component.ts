import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { LocalizationPipe, SessionStateService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { interval } from 'rxjs';
import { CustomerSmartOffersService } from '../../../proxy/controllers/customer-smart-offers.service';
import type { CustomerSmartOfferOrderDto } from '../../../proxy/smart-offers/models';
import { CustomerDealOrderFilter } from '../../../proxy/smart-offers/customer-deal-order-filter.enum';
import { SmartOfferOrderStatus } from '../../../proxy/smart-offers/smart-offer-order-status.enum';
import { Currency } from '../../../proxy/shared/currency.enum';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { PaginationComponent } from '../../../shared/components/pagination/pagination.component';
import { SkeletonListComponent } from '../../../shared/components/skeleton-list/skeleton-list.component';
import { StatusBadgeComponent, StatusBadgeVariant } from '../../../shared/components/status-badge/status-badge.component';
import { formatCountdown, formatSmartPrice } from '../../../shared/utils/smart-offer-display.util';

const PAGE_SIZE = 20;

// While an order is on hold, re-read the list this often, so a cashier's completion or rejection shows up on the phone.
const OPEN_HOLD_REFRESH_SECONDS = 30;

/**
 * The customer's own smart-deal orders across every business. The filter is applied on the server (the history is paged,
 * so a client-side filter would only see one page). Holds show their code and the time left, measured against the
 * server's clock so a phone with a wrong time still counts down correctly.
 */
@Component({
  selector: 'app-customer-deal-orders',
  templateUrl: './customer-deal-orders.component.html',
  styleUrls: ['./customer-deal-orders.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    LocalizationPipe,
    DatePipe,
    EmptyStateComponent,
    ErrorStateComponent,
    PaginationComponent,
    RouterLink,
    SkeletonListComponent,
    StatusBadgeComponent,
  ],
})
export class CustomerDealOrdersComponent implements OnInit {
  private readonly customerSmartOffersService = inject(CustomerSmartOffersService);
  private readonly toaster = inject(ToasterService);
  private readonly sessionState = inject(SessionStateService);

  protected readonly Filter = CustomerDealOrderFilter;
  protected readonly Status = SmartOfferOrderStatus;
  protected readonly Currency = Currency;
  protected readonly formatCountdown = formatCountdown;

  protected readonly filterOptions: { value: CustomerDealOrderFilter; labelKey: string; emptyKey: string }[] = [
    { value: CustomerDealOrderFilter.All, labelKey: '::SmartDeals:Browse:FilterAll', emptyKey: '::SmartDeals:Orders:EmptyAll' },
    { value: CustomerDealOrderFilter.Active, labelKey: '::SmartDeals:Orders:FilterActive', emptyKey: '::SmartDeals:Orders:EmptyActive' },
    { value: CustomerDealOrderFilter.Completed, labelKey: '::SmartDeals:Orders:FilterCompleted', emptyKey: '::SmartDeals:Orders:EmptyCompleted' },
    { value: CustomerDealOrderFilter.Closed, labelKey: '::SmartDeals:Orders:FilterClosed', emptyKey: '::SmartDeals:Orders:EmptyClosed' },
  ];

  protected readonly language = toSignal(this.sessionState.getLanguage$(), {
    initialValue: this.sessionState.getLanguage(),
  });

  protected readonly filter = signal<CustomerDealOrderFilter>(CustomerDealOrderFilter.All);
  protected readonly orders = signal<CustomerSmartOfferOrderDto[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly pageIndex = signal(0);
  protected readonly totalPages = computed(() => Math.max(1, Math.ceil(this.totalCount() / PAGE_SIZE)));
  protected readonly isLoading = signal(true);
  protected readonly loadFailed = signal(false);
  protected readonly busyOrderId = signal<string | null>(null);

  // Countdowns use the server's clock as of the last response, moved forward by the time elapsed on this device.
  private readonly serverOffsetMs = signal(0);
  private readonly nowMs = signal(Date.now());
  private readonly serverNowMs = computed(() => this.nowMs() + this.serverOffsetMs());

  private tickCount = 0;

  protected readonly hasOpenHold = computed(() => this.orders().some(order => this.isOpen(order)));

  constructor() {
    interval(1000)
      .pipe(takeUntilDestroyed())
      .subscribe(() => {
        this.nowMs.set(Date.now());
        this.tickCount++;
        if (this.hasOpenHold() && this.tickCount % OPEN_HOLD_REFRESH_SECONDS === 0) {
          this.load(false);
        }
      });
  }

  ngOnInit(): void {
    this.load(true);
  }

  protected retry(): void {
    this.load(true);
  }

  protected setFilter(filter: CustomerDealOrderFilter): void {
    if (this.filter() === filter) return;
    this.filter.set(filter);
    this.pageIndex.set(0);
    this.load(true);
  }

  protected goToPage(index: number): void {
    this.pageIndex.set(index);
    this.load(true);
  }

  /** A hold that is still open: pending on the server and not yet lapsed on the server's clock. */
  protected isOpen(order: CustomerSmartOfferOrderDto): boolean {
    return order.status === SmartOfferOrderStatus.Pending && this.holdLeftMs(order) > 0;
  }

  protected holdLeftMs(order: CustomerSmartOfferOrderDto): number {
    return order.reservationExpiresAt ? new Date(order.reservationExpiresAt).getTime() - this.serverNowMs() : 0;
  }

  protected title(order: CustomerSmartOfferOrderDto): string {
    return this.language() === 'ar'
      ? order.offerTitleAr || order.offerTitleEn || ''
      : order.offerTitleEn || order.offerTitleAr || '';
  }

  // Same language rule as title(): the deal's own language first, the other one only when it is missing.
  protected description(order: CustomerSmartOfferOrderDto): string {
    return this.language() === 'ar'
      ? order.offerDescriptionAr || order.offerDescriptionEn || ''
      : order.offerDescriptionEn || order.offerDescriptionAr || '';
  }

  protected money(amount: number | null | undefined, currency: Currency | undefined): string {
    return amount == null ? '' : formatSmartPrice(amount, currency, this.language());
  }

  protected statusLabelKey(status: SmartOfferOrderStatus | undefined): string {
    switch (status) {
      case SmartOfferOrderStatus.Pending:
        return '::SmartDeals:Order:StatusPending';
      case SmartOfferOrderStatus.Completed:
        return '::SmartDeals:Order:StatusCompleted';
      case SmartOfferOrderStatus.Rejected:
        return '::SmartDeals:Order:StatusRejected';
      case SmartOfferOrderStatus.Cancelled:
        return '::SmartDeals:Order:StatusCancelled';
      default:
        return '::SmartDeals:Order:StatusExpired';
    }
  }

  protected statusVariant(status: SmartOfferOrderStatus | undefined): StatusBadgeVariant {
    switch (status) {
      case SmartOfferOrderStatus.Pending:
        return 'info';
      case SmartOfferOrderStatus.Completed:
        return 'success';
      case SmartOfferOrderStatus.Rejected:
      case SmartOfferOrderStatus.Expired:
        return 'danger';
      default:
        return 'neutral';
    }
  }

  protected emptyKey(): string {
    return this.filterOptions.find(option => option.value === this.filter())?.emptyKey ?? '::SmartDeals:Orders:EmptyAll';
  }

  protected cancel(order: CustomerSmartOfferOrderDto): void {
    if (!order.id || !order.tenantId || this.busyOrderId()) return;

    this.busyOrderId.set(order.id);
    this.customerSmartOffersService.cancelMyOrder(order.tenantId, order.id).subscribe({
      next: updated => {
        this.orders.update(list => list.map(o => (o.id === updated.id ? { ...o, ...updated } : o)));
        this.busyOrderId.set(null);
        this.toaster.success('::SmartDeals:Orders:CancelledMessage');
      },
      error: () => this.busyOrderId.set(null),
    });
  }

  private load(showSpinner: boolean): void {
    if (showSpinner) {
      this.isLoading.set(true);
    }
    this.loadFailed.set(false);

    this.customerSmartOffersService
      .getMyOrders({
        sorting: 'creationTime desc',
        skipCount: this.pageIndex() * PAGE_SIZE,
        maxResultCount: PAGE_SIZE,
        filter: this.filter(),
      })
      .subscribe({
        next: result => {
          const items = result.items ?? [];
          this.orders.set(items);
          this.totalCount.set(result.totalCount ?? 0);
          if (items[0]?.serverNowUtc) {
            this.serverOffsetMs.set(new Date(items[0].serverNowUtc).getTime() - Date.now());
          }
          this.isLoading.set(false);
        },
        error: () => {
          this.isLoading.set(false);
          this.loadFailed.set(true);
        },
      });
  }
}
