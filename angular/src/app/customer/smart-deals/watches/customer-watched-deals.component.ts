import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { LocalizationPipe } from '@abp/ng.core';
import { CustomerSmartOffersService } from '../../../proxy/controllers/customer-smart-offers.service';
import type { CustomerSmartOfferDto } from '../../../proxy/smart-offers/models';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { SkeletonListComponent } from '../../../shared/components/skeleton-list/skeleton-list.component';
import { SmartDealCardComponent } from '../../../shared/components/smart-deal-card/smart-deal-card.component';
import { SmartOfferWatchService } from '../../../shared/services/smart-offer-watch.service';

/**
 * The deals the customer has asked to be told about when the price drops — the "My Price Watches" list
 * explicitly requested, since the toggle on a deal card (smart-deal-card.component.ts) only lets a
 * customer watch/unwatch one deal at a time and has never had a page that lists what's being watched.
 *
 * No dedicated backend endpoint was needed: GetMyPriceWatchesAsync only returns the watch keys
 * (tenantId + offerId, see SmartOfferWatchDto's own comment), not enough to render a card, so this page
 * reuses the same GetFeedAsync call the Browse page already makes (every live/upcoming deal across the
 * businesses the customer joined or follows) and filters it down to the ones SmartOfferWatchService
 * says are watched. A watch on a deal that has since been disabled outright (not just changed price,
 * which already clears the watch via SmartOfferPriceWatchWorker) would be invisible here until it's
 * cleaned up — an accepted edge case, not a page the app needs a second query just to catch.
 *
 * Unwatching from this page removes the card immediately: `watched()` is reactive, so the
 * `canWatchPrice`/`toggleWatch` round-trip inside smart-deal-card.component.ts updates the same shared
 * signal this page's `watchedDeals` is computed from.
 */
@Component({
  selector: 'app-customer-watched-deals',
  templateUrl: './customer-watched-deals.component.html',
  styleUrls: ['./customer-watched-deals.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [LocalizationPipe, EmptyStateComponent, ErrorStateComponent, SkeletonListComponent, SmartDealCardComponent],
})
export class CustomerWatchedDealsComponent implements OnInit {
  private readonly customerSmartOffersService = inject(CustomerSmartOffersService);
  protected readonly watchService = inject(SmartOfferWatchService);

  protected readonly feed = signal<CustomerSmartOfferDto[]>([]);
  protected readonly isLoading = signal(true);
  protected readonly loadFailed = signal(false);

  protected readonly watchedDeals = computed(() =>
    this.feed().filter(deal => !!deal.id && !!deal.tenantId && this.watchService.isWatched(deal.tenantId, deal.id)),
  );

  // Both the feed and the watch list load independently; only once both have answered do we know whether
  // "no watched deals" is the real empty state or just the first response still in flight.
  protected readonly ready = computed(() => !this.isLoading() && this.watchService.loaded());

  ngOnInit(): void {
    this.watchService.ensureLoaded();
    this.load();
  }

  protected retry(): void {
    this.load();
  }

  private load(): void {
    this.isLoading.set(true);
    this.loadFailed.set(false);
    this.customerSmartOffersService.getFeed(200).subscribe({
      next: result => {
        this.feed.set(result.items ?? []);
        this.isLoading.set(false);
      },
      error: () => {
        this.isLoading.set(false);
        this.loadFailed.set(true);
      },
    });
  }
}
