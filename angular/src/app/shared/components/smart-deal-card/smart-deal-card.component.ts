import { ChangeDetectionStrategy, Component, computed, effect, inject, input, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { LocalizationPipe, SessionStateService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { interval } from 'rxjs';
import { CustomerSmartOffersService } from '../../../proxy/controllers/customer-smart-offers.service';
import type { CustomerSmartOfferDto, SmartOfferOrderDto } from '../../../proxy/smart-offers/models';
import { SmartOfferOrderStatus } from '../../../proxy/smart-offers/smart-offer-order-status.enum';
import { SmartOfferStatus } from '../../../proxy/smart-offers/smart-offer-status.enum';
import { Currency } from '../../../proxy/shared/currency.enum';
import { formatCountdown, formatSmartPrice } from '../../utils/smart-offer-display.util';
import { ENDING_SOON_MINUTES } from '../../utils/smart-deal-feed.util';
import { SmartOfferWatchService } from '../../services/smart-offer-watch.service';

// How often the order is re-read while a reservation is open. The countdown itself ticks every second, so this only
// decides how quickly a counter-side completion or rejection shows up on the customer's phone.
const ORDER_POLL_SECONDS = 15;

/**
 * One Smart Deal as a customer sees it. The card shows, and never works out, prices, windows or stock: every figure
 * comes from CustomerSmartOfferDto, computed on the server at serverNowUtc. The countdowns are measured against that
 * server instant, so a phone with a wrong clock still shows the right time left.
 */
@Component({
  selector: 'app-smart-deal-card',
  templateUrl: './smart-deal-card.component.html',
  styleUrls: ['./smart-deal-card.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [LocalizationPipe, DatePipe, RouterLink],
})
export class SmartDealCardComponent {
  readonly offer = input.required<CustomerSmartOfferDto>();
  readonly tenantId = input.required<string>();

  /** A deal can only be bought by a member of the business. Browsing is open to everyone. */
  readonly canBuy = input(false);

  /** Shows the business name above the title. On for the browse feed, where deals come from many businesses. */
  readonly showBusiness = input(false);

  private readonly customerSmartOffersService = inject(CustomerSmartOffersService);
  private readonly sessionState = inject(SessionStateService);
  private readonly toaster = inject(ToasterService);

  protected readonly Currency = Currency;
  protected readonly OrderStatus = SmartOfferOrderStatus;
  protected readonly Status = SmartOfferStatus;
  protected readonly formatCountdown = formatCountdown;

  protected readonly language = toSignal(this.sessionState.getLanguage$(), {
    initialValue: this.sessionState.getLanguage(),
  });

  protected readonly watchService = inject(SmartOfferWatchService);

  // Watching is offered for a deal whose next change is a drop or a return to sale, to members only: a non-member
  // cannot watch a business's prices (the server refuses it), so the button is not shown to them.
  protected readonly canWatchPrice = computed(
    () => this.canBuy() && !this.pendingOrder() && (this.nextChangeKind() === 'drops' || this.nextChangeKind() === 'backOnSale'),
  );
  protected readonly watched = computed(() => this.watchService.isWatched(this.tenantId(), this.offer().id ?? ''));

  protected readonly quantity = signal(1);
  protected readonly isBusy = signal(false);
  protected readonly pendingOrder = signal<SmartOfferOrderDto | null>(null);
  protected readonly priceDropped = signal(false);

  // The server's "now" at the last response, plus the time elapsed on this device since. Countdowns use this, not the
  // device clock.
  private readonly serverOffsetMs = signal(0);
  private readonly nowMs = signal(Date.now());
  protected readonly serverNowMs = computed(() => this.nowMs() + this.serverOffsetMs());

  protected readonly title = computed(() => {
    const deal = this.offer();
    return this.language() === 'ar' ? deal.titleAr || deal.titleEn : deal.titleEn || deal.titleAr;
  });

  protected readonly description = computed(() => {
    const deal = this.offer();
    return this.language() === 'ar' ? deal.descriptionAr || deal.descriptionEn : deal.descriptionEn || deal.descriptionAr;
  });

  protected readonly remaining = computed(() => this.offer().remainingNow ?? null);

  protected readonly canOrderNow = computed(() => {
    const deal = this.offer();
    const remaining = this.remaining();
    return !!deal.isAvailableNow && deal.currentPrice != null && (remaining === null || remaining > 0);
  });

  protected readonly maxQuantity = computed(() => {
    const deal = this.offer();
    const cap = deal.maxOrderQuantity ?? 1;
    const remaining = this.remaining();
    return remaining === null ? cap : Math.max(1, Math.min(cap, remaining));
  });

  protected readonly orderTotal = computed(() => {
    const price = this.offer().currentPrice ?? 0;
    return price * this.quantity();
  });

  protected readonly nextChangeInMs = computed(() => this.msUntil(this.offer().nextChangeAtUtc));
  protected readonly stageEndsInMs = computed(() => this.msUntil(this.offer().currentStageEndsAtUtc));

  /** The live stage ends within the hour. Measured on the server's clock, like every other countdown on the card. */
  protected readonly endingSoon = computed(() => {
    const left = this.stageEndsInMs();
    return this.offer().isAvailableNow === true && left !== null && left > 0 && left <= ENDING_SOON_MINUTES * 60_000;
  });
  protected readonly holdLeftMs = computed(() => this.msUntil(this.pendingOrder()?.reservationExpiresAt));

  /** Whole-percent saving, or null when there is no saving to advertise. Comes from the server. */
  protected readonly discount = computed(() => {
    const percent = this.offer().discountPercent;
    return percent && percent > 0 ? percent : null;
  });

  /** Which next-change sentence to show. Decided from the server's own next price and current state. */
  protected readonly nextChangeKind = computed<'backOnSale' | 'offSale' | 'drops' | 'rises' | null>(() => {
    const deal = this.offer();
    if (deal.nextChangeAtUtc == null) return null;

    if (!deal.isAvailableNow) {
      return deal.nextPrice != null ? 'backOnSale' : null;
    }

    if (deal.nextPrice == null) return 'offSale';
    if (deal.currentPrice != null && deal.nextPrice < deal.currentPrice) return 'drops';
    if (deal.currentPrice != null && deal.nextPrice > deal.currentPrice) return 'rises';
    return null;
  });

  private previousPrice: number | null = null;
  private pollTick = 0;

  constructor() {
    this.watchService.ensureLoaded();

    // A single one-second heartbeat drives every countdown, and also re-reads an open order every so often.
    interval(1000)
      .pipe(takeUntilDestroyed())
      .subscribe(() => {
        this.nowMs.set(Date.now());
        this.pollTick++;
        if (this.pendingOrder() && this.pollTick % ORDER_POLL_SECONDS === 0) {
          this.refreshOrder();
        }
      });

    // Re-anchor to the server clock on every fresh response, and pick up an order the server already holds.
    effect(() => {
      const deal = this.offer();
      if (deal.serverNowUtc) {
        this.serverOffsetMs.set(new Date(deal.serverNowUtc).getTime() - Date.now());
      }
      this.pendingOrder.set(deal.myPendingOrder ?? null);
      this.quantity.update(q => Math.min(Math.max(1, q), this.maxQuantity()));
    });

    // Flash the price when it has fallen since the last time it was shown.
    effect(() => {
      const price = this.offer().currentPrice ?? null;
      if (this.previousPrice !== null && price !== null && price < this.previousPrice) {
        this.priceDropped.set(true);
        setTimeout(() => this.priceDropped.set(false), 1400);
      }
      this.previousPrice = price;
    });
  }

  protected toggleWatch(): void {
    this.watchService.toggle(this.tenantId(), this.offer().id ?? '');
  }

  protected money(amount: number | null | undefined, currency: Currency | undefined): string {
    return amount == null ? '' : formatSmartPrice(amount, currency, this.language());
  }

  protected step(delta: number): void {
    this.quantity.update(q => Math.min(this.maxQuantity(), Math.max(1, q + delta)));
  }

  protected buy(): void {
    if (this.isBusy() || !this.canBuy() || !this.canOrderNow()) return;

    const deal = this.offer();
    this.isBusy.set(true);
    this.customerSmartOffersService
      .placeOrder({ tenantId: this.tenantId(), smartOfferId: deal.id ?? '', quantity: this.quantity() })
      .subscribe({
        next: order => {
          this.isBusy.set(false);
          this.pendingOrder.set(order);
        },
        // The interceptor has already shown the server's reason (sold out, price ended, not a member).
        error: () => this.isBusy.set(false),
      });
  }

  protected cancelOrder(): void {
    const order = this.pendingOrder();
    if (!order?.id || this.isBusy()) return;

    this.isBusy.set(true);
    this.customerSmartOffersService.cancelMyOrder(this.tenantId(), order.id).subscribe({
      next: () => {
        this.isBusy.set(false);
        this.pendingOrder.set(null);
      },
      error: () => this.isBusy.set(false),
    });
  }

  protected dismissOrder(): void {
    this.pendingOrder.set(null);
  }

  /** A reservation whose window has closed on the server's clock is shown as closed, even before a re-read. */
  protected nextChangeTime(): string {
    return this.offer().nextChangeLocalTime ?? '';
  }

  protected holdIsOpen(order: SmartOfferOrderDto): boolean {
    return order.status === SmartOfferOrderStatus.Pending && (this.holdLeftMs() ?? 0) > 0;
  }

  private refreshOrder(): void {
    const order = this.pendingOrder();
    if (!order?.id) return;

    this.customerSmartOffersService.getMyOrder(this.tenantId(), order.id).subscribe({
      next: latest => {
        this.pendingOrder.set(latest);
        if (latest.status === SmartOfferOrderStatus.Completed) {
          this.toaster.success('::SmartDeals:Customer:CollectedMessage');
        }
      },
      error: () => undefined,
    });
  }

  private msUntil(iso: string | null | undefined): number | null {
    return iso ? new Date(iso).getTime() - this.serverNowMs() : null;
  }
}
