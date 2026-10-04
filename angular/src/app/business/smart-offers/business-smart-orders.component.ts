import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { LocalizationPipe, SessionStateService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { SmartOfferOrdersService } from '../../proxy/controllers/smart-offer-orders.service';
import type { SmartOfferOrderStaffDto } from '../../proxy/smart-offers/models';
import { SmartOfferOrderStatus } from '../../proxy/smart-offers/smart-offer-order-status.enum';
import { DatePipe } from '@angular/common';
import { ModalComponent } from '../../shared/components/modal/modal.component';
import { PaginationComponent } from '../../shared/components/pagination/pagination.component';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { SmartSaleDetailsComponent } from '../../shared/components/smart-sale-details/smart-sale-details.component';
import { StatusBadgeComponent, StatusBadgeVariant } from '../../shared/components/status-badge/status-badge.component';
import { formatCountdown, formatSmartPrice } from '../../shared/utils/smart-offer-display.util';

// The recent-sales list is paged, so a busy counter can look back through every sale, not only the last page.
const HISTORY_PAGE_SIZE = 10;

/**
 * Staff counter for Buy Now pickups. A customer shows a code; staff look it up, check the person and the amount, then
 * complete it (the sale is recorded) or reject it (the units go back on sale). Role checks happen on the server
 * (SmartOfferOrderAppService), so this page only decides what to offer, never what is allowed.
 */
@Component({
  selector: 'app-business-smart-orders',
  templateUrl: './business-smart-orders.component.html',
  styleUrls: ['./business-smart-orders.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    LocalizationPipe,
    DatePipe,
    PageHeaderComponent,
    StatusBadgeComponent,
    ModalComponent,
    PaginationComponent,
    SmartSaleDetailsComponent,
  ],
})
export class BusinessSmartOrdersComponent implements OnInit {
  private readonly smartOrdersService = inject(SmartOfferOrdersService);
  private readonly toaster = inject(ToasterService);
  private readonly sessionState = inject(SessionStateService);

  protected readonly Status = SmartOfferOrderStatus;

  protected readonly language = toSignal(this.sessionState.getLanguage$(), {
    initialValue: this.sessionState.getLanguage(),
  });

  protected readonly code = signal('');
  protected readonly reason = signal('');
  protected readonly order = signal<SmartOfferOrderStaffDto | null>(null);
  protected readonly isBusy = signal(false);

  // Settled sales, newest first. Loaded from the server so the list survives a refresh.
  protected readonly history = signal<SmartOfferOrderStaffDto[]>([]);
  protected readonly historyLoaded = signal(false);
  protected readonly historyFailed = signal(false);
  protected readonly historyPageIndex = signal(0);
  protected readonly historyTotal = signal(0);
  protected readonly historyPages = computed(() => Math.max(1, Math.ceil(this.historyTotal() / HISTORY_PAGE_SIZE)));

  // The history row whose details are open. Null while the details modal is closed.
  protected readonly selectedSale = signal<SmartOfferOrderStaffDto | null>(null);

  protected readonly isPending = computed(() => this.order()?.status === SmartOfferOrderStatus.Pending);

  ngOnInit(): void {
    this.loadHistory();
  }

  protected goToHistoryPage(index: number): void {
    if (index < 0 || index >= this.historyPages()) return;
    this.historyPageIndex.set(index);
    this.loadHistory();
  }

  protected loadHistory(): void {
    this.historyFailed.set(false);
    this.smartOrdersService.getHistory({
      sorting: 'creationTime desc',
      skipCount: this.historyPageIndex() * HISTORY_PAGE_SIZE,
      maxResultCount: HISTORY_PAGE_SIZE,
    }).subscribe({
      next: result => {
        this.history.set(result.items ?? []);
        this.historyTotal.set(result.totalCount ?? 0);
        this.historyLoaded.set(true);
      },
      // A failed load is reported as a failure. Showing it as "no sales" would hide a broken request.
      error: () => {
        this.historyFailed.set(true);
        this.historyLoaded.set(true);
      },
    });
  }

  protected money(amount: number | null | undefined, currency: SmartOfferOrderStaffDto['currency']): string {
    return amount == null ? '' : formatSmartPrice(amount, currency, this.language());
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

  /** Time left on the hold, measured on the server's clock so a skewed till does not show a wrong figure. */
  protected validFor(order: SmartOfferOrderStaffDto): string {
    if (!order.reservationExpiresAt || !order.serverNowUtc) return '';
    const remaining = new Date(order.reservationExpiresAt).getTime() - new Date(order.serverNowUtc).getTime();
    return formatCountdown(remaining);
  }

  protected lookup(): void {
    const code = this.code().trim();
    if (!code || this.isBusy()) return;

    this.isBusy.set(true);
    this.smartOrdersService.lookup({ code }).subscribe({
      next: order => {
        this.order.set(order);
        this.reason.set('');
        this.isBusy.set(false);
      },
      error: () => this.isBusy.set(false),
    });
  }

  protected complete(): void {
    const current = this.order();
    if (!current?.code || this.isBusy()) return;

    this.isBusy.set(true);
    this.smartOrdersService.complete({ code: current.code }).subscribe({
      next: order => {
        this.order.set(order);
        this.isBusy.set(false);
        // A new sale is the newest row, so the history goes back to its first page to show it.
        this.historyPageIndex.set(0);
        this.loadHistory();
        this.toaster.success('::SmartDeals:Order:CompletedMessage');
      },
      error: () => this.isBusy.set(false),
    });
  }

  protected reject(): void {
    const current = this.order();
    if (!current?.code || this.isBusy()) return;

    this.isBusy.set(true);
    this.smartOrdersService.reject({ code: current.code, reason: this.reason().trim() || null }).subscribe({
      next: order => {
        this.order.set(order);
        this.isBusy.set(false);
        this.historyPageIndex.set(0);
        this.loadHistory();
        this.toaster.success('::SmartDeals:Order:RejectedMessage');
      },
      error: () => this.isBusy.set(false),
    });
  }

  protected openSale(sale: SmartOfferOrderStaffDto): void {
    this.selectedSale.set(sale);
  }

  protected closeSale(): void {
    this.selectedSale.set(null);
  }

  protected newOrder(): void {
    this.code.set('');
    this.reason.set('');
    this.order.set(null);
  }

  protected onCodeInput(event: Event): void {
    this.code.set((event.target as HTMLInputElement).value);
  }

  protected onReasonInput(event: Event): void {
    this.reason.set((event.target as HTMLInputElement).value);
  }
}
