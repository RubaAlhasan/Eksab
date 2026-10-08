import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { LocalizationPipe, SessionStateService } from '@abp/ng.core';
import { toSignal } from '@angular/core/rxjs-interop';
import { CouponsService } from '../../proxy/controllers/coupons.service';
import type { CustomerRewardDto } from '../../proxy/rewards/models';
import { SkeletonListComponent } from '../../shared/components/skeleton-list/skeleton-list.component';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../shared/components/error-state/error-state.component';
import { PaginationComponent } from '../../shared/components/pagination/pagination.component';
import { SearchInputComponent } from '../../shared/components/search-input/search-input.component';
import { rewardTypeEmoji } from '../../shared/utils/reward-display.util';

// The feed is loaded once in full (same shape CustomerSmartOffersService.getFeed's own Browse page
// uses) and filtered/sorted/paged in the browser from there.
const FEED_SIZE = 200;
const PAGE_SIZE = 12;

type RewardSort = 'affordableFirst' | 'cheapest' | 'mostExpensive';

/**
 * "My Rewards" — every reward across every business the customer is an active, approved member of,
 * not just one business at a time (that's still `customer-rewards-catalog.component.ts`, reached from
 * a specific business's own wallet). Backend: `ICouponAppService.GetMyFeedAsync`, the same
 * disable-IMultiTenant + approved-membership selection shape
 * `CustomerSmartOfferAppService.GetFeedAsync`/`CustomerCampaignAppService.GetMyFeedAsync` already use.
 *
 * `CanAfford`/`PointsNeeded`/`AvailableBalance` are server-computed per item (each reward's own
 * business's wallet, not a single shared balance) — never derived client-side the way the per-business
 * catalog page has to (that page only has one wallet to check against). Defaults to "Affordable only",
 * matching how this page was asked for, with a toggle to browse everything including what's still out
 * of reach, each row then showing how many more points it needs.
 */
@Component({
  selector: 'app-customer-rewards-feed',
  templateUrl: './customer-rewards-feed.component.html',
  styleUrls: ['./customer-rewards-feed.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink,
    LocalizationPipe,
    SkeletonListComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    PaginationComponent,
    SearchInputComponent,
  ],
})
export class CustomerRewardsFeedComponent implements OnInit {
  private readonly couponsService = inject(CouponsService);
  private readonly sessionState = inject(SessionStateService);

  protected readonly rewardTypeEmoji = rewardTypeEmoji;
  protected readonly language = toSignal(this.sessionState.getLanguage$(), {
    initialValue: this.sessionState.getLanguage(),
  });

  protected readonly feed = signal<CustomerRewardDto[]>([]);
  protected readonly isLoading = signal(true);
  protected readonly loadFailed = signal(false);

  protected readonly affordableOnly = signal(true);
  protected readonly query = signal('');
  protected readonly sort = signal<RewardSort>('affordableFirst');
  protected readonly pageIndex = signal(0);

  protected readonly affordableCount = computed(() => this.feed().filter(r => r.canAfford).length);

  protected readonly filtered = computed(() => {
    const query = this.query().trim().toLowerCase();
    return this.feed().filter(r => {
      if (this.affordableOnly() && !r.canAfford) return false;
      if (!query) return true;
      return (
        r.nameEn.toLowerCase().includes(query) ||
        r.nameAr.toLowerCase().includes(query) ||
        r.businessName.toLowerCase().includes(query)
      );
    });
  });

  protected readonly sorted = computed(() => {
    const items = [...this.filtered()];
    switch (this.sort()) {
      case 'cheapest':
        return items.sort((a, b) => a.pointsCost - b.pointsCost);
      case 'mostExpensive':
        return items.sort((a, b) => b.pointsCost - a.pointsCost);
      default:
        // Server already returns affordable-first, cheapest-within-group; stable sort keeps that order.
        return items;
    }
  });

  protected readonly totalPages = computed(() => Math.max(1, Math.ceil(this.sorted().length / PAGE_SIZE)));
  protected readonly pageItems = computed(() => {
    const start = this.pageIndex() * PAGE_SIZE;
    return this.sorted().slice(start, start + PAGE_SIZE);
  });

  protected name(reward: CustomerRewardDto): string {
    return this.language() === 'ar' ? reward.nameAr || reward.nameEn : reward.nameEn || reward.nameAr;
  }

  protected isOutOfStock(reward: CustomerRewardDto): boolean {
    return reward.stockRemaining != null && reward.stockRemaining <= 0;
  }

  protected isLowStock(reward: CustomerRewardDto): boolean {
    return reward.stockRemaining != null && reward.stockRemaining > 0 && reward.stockRemaining < 10;
  }

  ngOnInit(): void {
    this.load();
  }

  protected retry(): void {
    this.load();
  }

  protected setAffordableOnly(value: boolean): void {
    this.affordableOnly.set(value);
    this.pageIndex.set(0);
  }

  protected setQuery(value: string): void {
    this.query.set(value);
    this.pageIndex.set(0);
  }

  protected setSort(value: string): void {
    this.sort.set(value as RewardSort);
    this.pageIndex.set(0);
  }

  protected goToPage(index: number): void {
    if (index < 0 || index >= this.totalPages()) return;
    this.pageIndex.set(index);
  }

  private load(): void {
    this.isLoading.set(true);
    this.loadFailed.set(false);
    this.couponsService.getMyFeed(FEED_SIZE).subscribe({
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
