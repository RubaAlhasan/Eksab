import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { interval } from 'rxjs';
import { RouterLink } from '@angular/router';
import { LocalizationPipe, SessionStateService } from '@abp/ng.core';
import { CustomerSmartOffersService } from '../../proxy/controllers/customer-smart-offers.service';
import type { CustomerSmartOfferDto } from '../../proxy/smart-offers/models';
import { Currency } from '../../proxy/shared/currency.enum';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../shared/components/error-state/error-state.component';
import { SearchInputComponent } from '../../shared/components/search-input/search-input.component';
import { PaginationComponent } from '../../shared/components/pagination/pagination.component';
import { SkeletonListComponent } from '../../shared/components/skeleton-list/skeleton-list.component';
import { SmartDealCardComponent } from '../../shared/components/smart-deal-card/smart-deal-card.component';
import {
  DEFAULT_DEAL_FILTERS,
  DealAvailability,
  DealFilters,
  DealSection,
  DealSort,
  MinDiscount,
  filterDeals,
  groupDeals,
  serverNowMs,
  sortDeals,
} from '../../shared/utils/smart-deal-feed.util';

// The browse feed asks for more than the home strip does. Filtering happens on this list, so it should hold everything
// a customer is likely to scan.
const FEED_SIZE = 200;

// One page of deals on the browse list. Filtering and sorting run on the whole feed first, so each page is a slice of the
// full result and the pager counts the matching deals, not the feed.
const PAGE_SIZE = 12;

// Groups are re-evaluated this often, so a deal that starts to end moves into "ending soon" without a reload.
const REGROUP_SECONDS = 30;

/**
 * Smart deals across every business the customer joined or follows. Filters and ordering run on the list the server
 * returned (see smart-deal-feed.util); the page never prices anything itself. With no filter set, deals are grouped into
 * "Ending soon", "Live now" and "Coming up". Any filter switches to one flat list, so a narrowed view reads as a result.
 */
@Component({
  selector: 'app-customer-smart-deals',
  templateUrl: './customer-smart-deals.component.html',
  styleUrls: ['./customer-smart-deals.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink,
    LocalizationPipe,
    EmptyStateComponent,
    ErrorStateComponent,
    SearchInputComponent,
    PaginationComponent,
    SkeletonListComponent,
    SmartDealCardComponent,
  ],
})
export class CustomerSmartDealsComponent implements OnInit {
  private readonly customerSmartOffersService = inject(CustomerSmartOffersService);
  private readonly sessionState = inject(SessionStateService);

  protected readonly Currency = Currency;
  protected readonly discountSteps: MinDiscount[] = [0, 10, 25, 50];
  protected readonly language = toSignal(this.sessionState.getLanguage$(), {
    initialValue: this.sessionState.getLanguage(),
  });

  protected readonly feed = signal<CustomerSmartOfferDto[]>([]);
  protected readonly isLoading = signal(true);
  protected readonly loadFailed = signal(false);
  protected readonly filters = signal<DealFilters>(DEFAULT_DEAL_FILTERS);

  protected readonly filtered = computed(() => filterDeals(this.feed(), this.filters()));

  /** True when no narrowing filter is set, which is the only case the grouped view is shown for. */
  protected readonly isGrouped = computed(() => {
    const filters = this.filters();
    return filters.availability === 'all' && filters.query.trim() === '' && filters.currency === null && filters.minDiscount === 0;
  });

  // The server's clock at the last response, moved forward by the time elapsed on this device.
  private readonly serverOffsetMs = signal(0);
  private readonly nowMs = signal(Date.now());
  private readonly serverNowMs = computed(() => this.nowMs() + this.serverOffsetMs());

  protected readonly pageIndex = signal(0);
  protected readonly sortedDeals = computed(() => sortDeals(this.filtered(), this.filters().sort));
  protected readonly totalPages = computed(() => Math.max(1, Math.ceil(this.sortedDeals().length / PAGE_SIZE)));
  protected readonly pageDeals = computed(() => {
    const start = this.pageIndex() * PAGE_SIZE;
    return this.sortedDeals().slice(start, start + PAGE_SIZE);
  });

  // The grouped view is built from the page on screen, so each page shows its own groups.
  protected readonly sections = computed<DealSection[]>(() => {
    const deals = this.pageDeals();
    const sort = this.filters().sort;
    const grouped = groupDeals(deals, this.serverNowMs());
    return [
      { key: 'endingSoon', icon: 'fa-hourglass-half', titleKey: '::SmartDeals:Browse:EndingSoon', deals: sortDeals(grouped.endingSoon, sort) },
      { key: 'liveNow', icon: 'fa-bolt', titleKey: '::SmartDeals:Browse:LiveNow', deals: sortDeals(grouped.liveNow, sort) },
      { key: 'comingUp', icon: 'fa-clock', titleKey: '::SmartDeals:Browse:ComingUp', deals: sortDeals(grouped.comingUp, sort) },
    ].filter(section => section.deals.length > 0) as DealSection[];
  });

  protected readonly flatList = computed(() => this.pageDeals());

  protected readonly liveCount = computed(() => this.feed().filter(deal => deal.isAvailableNow).length);

  constructor() {
    interval(REGROUP_SECONDS * 1000)
      .pipe(takeUntilDestroyed())
      .subscribe(() => this.nowMs.set(Date.now()));
  }

  ngOnInit(): void {
    this.load();
  }

  protected retry(): void {
    this.load();
  }

  protected goToPage(index: number): void {
    if (index < 0 || index >= this.totalPages()) return;
    this.pageIndex.set(index);
  }

  protected clearFilters(): void {
    this.applyFilters(() => DEFAULT_DEAL_FILTERS);
  }

  protected setQuery(query: string): void {
    this.applyFilters(f => ({ ...f, query }));
  }

  protected setAvailability(availability: DealAvailability): void {
    this.applyFilters(f => ({ ...f, availability }));
  }

  protected setCurrency(value: string): void {
    this.applyFilters(f => ({ ...f, currency: value === '' ? null : (Number(value) as Currency) }));
  }

  protected setMinDiscount(minDiscount: MinDiscount): void {
    this.applyFilters(f => ({ ...f, minDiscount }));
  }

  protected setSort(value: string): void {
    this.applyFilters(f => ({ ...f, sort: value as DealSort }));
  }

  // A new filter means a new result, so the pager starts again from the first page.
  private applyFilters(update: (filters: DealFilters) => DealFilters): void {
    this.filters.update(update);
    this.pageIndex.set(0);
  }

  private load(): void {
    this.isLoading.set(true);
    this.loadFailed.set(false);
    this.customerSmartOffersService.getFeed(FEED_SIZE).subscribe({
      next: result => {
        const items = result.items ?? [];
        this.feed.set(items);
        this.serverOffsetMs.set(serverNowMs(items) - Date.now());
        this.nowMs.set(Date.now());
        this.isLoading.set(false);
      },
      error: () => {
        this.isLoading.set(false);
        this.loadFailed.set(true);
      },
    });
  }
}
