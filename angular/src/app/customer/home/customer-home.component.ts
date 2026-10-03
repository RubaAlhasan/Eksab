import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ConfigStateService, LocalizationPipe } from '@abp/ng.core';
import { forkJoin, of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { MembershipsService } from '../../proxy/controllers/memberships.service';
import { CustomerProfileService } from '../../proxy/controllers/customer-profile.service';
import { CustomerCampaignService } from '../../proxy/controllers/customer-campaign.service';
import { CustomerBusinessService } from '../../proxy/controllers/customer-business.service';
import { WalletService } from '../../proxy/controllers/wallet.service';
import { CouponsService } from '../../proxy/controllers/coupons.service';
import { NotificationHubService } from '../../shared/services/notification-hub.service';
import type { PointsWalletDto } from '../../proxy/wallets/models';
import type { CustomerCampaignDto } from '../../proxy/campaigns/models';
import type { CustomerBusinessDto } from '../../proxy/businesses/models';
import type { CouponDto } from '../../proxy/rewards/models';
import { CouponStatus } from '../../proxy/rewards/coupon-status.enum';
import type { TransactionListItemDto } from '../../proxy/reports/models';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../shared/components/error-state/error-state.component';
import { campaignTypeEmoji, campaignTypeLabelKey } from '../../shared/utils/campaign-display.util';
import { isCredit, transactionSourceLabelKey, transactionTypeLabelKey } from '../../shared/utils/transaction-display.util';

const DISCOVER_CANDIDATE_COUNT = 8;
const DISCOVER_PREVIEW_COUNT = 4;
const CAMPAIGN_PREVIEW_COUNT = 6;
// How many of the customer's joined businesses to pull recent activity from — bounded so a member of
// many businesses doesn't fan out into dozens of parallel requests just to render a home-page preview.
const RECENT_ACTIVITY_WALLET_FANOUT = 6;
const RECENT_ACTIVITY_PER_WALLET = 3;
const RECENT_ACTIVITY_PREVIEW_COUNT = 5;
const REWARDS_PREVIEW_COUNT = 3;

// One row of the Home page's own cross-business "Recent Activity" feed — TransactionListItemDto itself
// has no business name (it's the same shape WalletService returns for a single, already-known tenant
// elsewhere), so this attaches it at the call site, where the tenant being queried is still known.
interface RecentActivityItem {
  transaction: TransactionListItemDto;
  businessName: string;
}

/**
 * Customer app Home tab — rebuilt to match the prototype's home.html: a greeting header with an
 * unread-aware notification bell, a "My Businesses" wallet carousel (still the same
 * `MembershipAppService.GetMyWalletsAsync` data the old plain grid used), an "Active Campaigns" preview
 * carousel, a two-tile quick-actions row, and a "Discover Nearby" preview grid of not-yet-joined
 * businesses. The prototype's third quick action ("Scan & Check-in", a customer self-scanning a branch's
 * check-in poster) has no backend behind it anywhere in this codebase — confirmed by grep, not just
 * missing a proxy like the other hand-wired endpoints — so it's swapped for "My Coupons", a real
 * destination, rather than shipping a dead button.
 *
 * Campaigns/Discover are preview-only widgets (first few items, silently empty on error rather than
 * their own loading/error UI) — only the wallets list keeps the full loading/error/empty treatment,
 * since it's the page's primary content and the one thing every customer has.
 */
@Component({
  selector: 'app-customer-home',
  templateUrl: './customer-home.component.html',
  styleUrls: ['./customer-home.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink,
    DecimalPipe,
    DatePipe,
    LocalizationPipe,
    EmptyStateComponent,
    ErrorStateComponent,
  ],
})
export class CustomerHomeComponent implements OnInit {
  private readonly membershipsService = inject(MembershipsService);
  private readonly customerProfileService = inject(CustomerProfileService);
  private readonly customerCampaignService = inject(CustomerCampaignService);
  private readonly customerBusinessService = inject(CustomerBusinessService);
  private readonly walletService = inject(WalletService);
  private readonly couponsService = inject(CouponsService);
  private readonly configState = inject(ConfigStateService);
  protected readonly hub = inject(NotificationHubService);

  protected readonly isLoading = signal(true);
  protected readonly loadFailed = signal(false);
  protected readonly wallets = signal<PointsWalletDto[]>([]);
  protected readonly displayName = signal<string | null>(null);

  protected readonly campaigns = signal<CustomerCampaignDto[]>([]);
  protected readonly discoverCandidates = signal<CustomerBusinessDto[]>([]);

  // Recent activity and rewards previews are genuinely secondary content (the hero stats and wallet
  // carousel above them are the page's primary content and already have full loading/error/empty
  // treatment) — both load silently and simply render nothing on failure or while empty, same
  // "preview widget" pattern campaigns/discover already use on this page.
  protected readonly recentActivityLoading = signal(true);
  protected readonly recentActivity = signal<RecentActivityItem[]>([]);
  protected readonly activeCoupons = signal<CouponDto[]>([]);

  protected readonly campaignTypeEmoji = campaignTypeEmoji;
  protected readonly campaignTypeLabelKey = campaignTypeLabelKey;
  protected readonly transactionTypeLabelKey = transactionTypeLabelKey;
  protected readonly transactionSourceLabelKey = transactionSourceLabelKey;
  protected readonly isCredit = isCredit;

  // Hero stats — derived entirely from `wallets`, already fetched for the carousel below; no extra
  // network calls for these three numbers.
  protected readonly totalPoints = computed(() => this.wallets().reduce((sum, w) => sum + (w.balance ?? 0), 0));
  protected readonly businessCount = computed(() => this.wallets().length);
  protected readonly activeCouponsCount = computed(() => this.activeCoupons().length);

  protected readonly rewardsPreview = computed(() => this.activeCoupons().slice(0, REWARDS_PREVIEW_COUNT));

  // The hero's split bar: each joined business's share of the total, largest first. Built from the wallet
  // balances already loaded for the carousel, so no extra request. Opacity steps down per segment so the
  // split reads as one hue rather than a rainbow, and works for any number of businesses.
  protected readonly balanceSegments = computed(() => {
    const total = this.totalPoints();
    if (total <= 0) return [];
    return this.wallets()
      .filter(wallet => (wallet.balance ?? 0) > 0)
      .sort((a, b) => (b.balance ?? 0) - (a.balance ?? 0))
      .map((wallet, index) => ({
        name: wallet.businessName ?? '',
        balance: wallet.balance ?? 0,
        share: ((wallet.balance ?? 0) / total) * 100,
        opacity: Math.max(0.35, 1 - index * 0.22),
      }));
  });

  // Coupons only ever exist for a business the customer already has a wallet with, so this reuses the
  // already-loaded wallet list rather than a new lookup call, same spirit as the hero stats above.
  private readonly businessNameByTenantId = computed(() => {
    const map = new Map<string, string>();
    for (const wallet of this.wallets()) {
      if (wallet.tenantId && wallet.businessName) map.set(wallet.tenantId, wallet.businessName);
    }
    return map;
  });

  protected rewardBusinessName(coupon: CouponDto): string | null {
    return coupon.tenantId ? (this.businessNameByTenantId().get(coupon.tenantId) ?? null) : null;
  }

  // Computed once per page view (not a signal) — a greeting that flips mid-session if the tab is left
  // open across noon is not worth the reactivity.
  protected readonly greetingKey = (() => {
    const hour = new Date().getHours();
    if (hour < 12) return '::Wallet:Home:GreetingMorning';
    if (hour < 18) return '::Wallet:Home:GreetingAfternoon';
    return '::Wallet:Home:GreetingEvening';
  })();

  // Falls back to the phone number on the token itself (CurrentUserDto.phoneNumber) for a brand-new
  // customer whose CustomerProfile has no name yet (OtpLoginService's auto-create-blank-profile
  // fallback).
  protected readonly greetingName = computed(() => {
    const name = this.displayName();
    if (name) return name;
    const currentUser = this.configState.getOne('currentUser') as { phoneNumber?: string } | undefined;
    return currentUser?.phoneNumber ?? '';
  });

  protected readonly campaignPreview = computed(() => this.campaigns().slice(0, CAMPAIGN_PREVIEW_COUNT));

  private readonly joinedTenantIds = computed(
    () => new Set(this.wallets().map(w => w.tenantId).filter((id): id is string => !!id)),
  );

  protected readonly discoverBusinesses = computed(() =>
    this.discoverCandidates()
      .filter(business => !this.joinedTenantIds().has(business.tenantId))
      .slice(0, DISCOVER_PREVIEW_COUNT),
  );

  ngOnInit(): void {
    this.customerProfileService.getMyProfile().subscribe({
      next: profile => {
        const fullName = [profile.firstName, profile.lastName].filter(Boolean).join(' ').trim();
        this.displayName.set(fullName || null);
      },
      error: () => undefined,
    });

    this.customerCampaignService.getMyFeed().subscribe({
      next: campaigns => this.campaigns.set(campaigns),
      error: () => undefined,
    });

    this.customerBusinessService
      .getList({
        filterText: null,
        categoryId: null,
        latitude: null,
        longitude: null,
        skipCount: 0,
        maxResultCount: DISCOVER_CANDIDATE_COUNT,
      })
      .subscribe({
        next: result => this.discoverCandidates.set(result.items ?? []),
        error: () => undefined,
      });

    this.couponsService.getMyCoupons().subscribe({
      next: coupons => {
        // "Active" matches customer-my-coupons.component.ts's own definition exactly: Issued and
        // Pending are both "not yet used, not expired" from the customer's point of view.
        this.activeCoupons.set(coupons.filter(c => c.status === CouponStatus.Issued || c.status === CouponStatus.Pending));
      },
      error: () => undefined,
    });

    this.load();
  }

  protected retry(): void {
    this.load();
  }

  // Per-card fallback state for Discover-preview logos (a Set keyed by tenantId) — same pattern as
  // CustomerDiscoverComponent/CustomerFavoritesComponent. Wallet cards have no logo field on
  // PointsWalletDto at all, so they always use the initials fallback, no tracking needed.
  protected readonly logoFailedIds = signal<Set<string>>(new Set());

  protected logoUrl(business: CustomerBusinessDto): string | null {
    if (!business.hasLogo || this.logoFailedIds().has(business.tenantId)) return null;
    return `${environment.apis.default.url}/api/app/business/${business.businessProfileId}/logo?v=${business.logoBlobName ?? ''}`;
  }

  protected markLogoFailed(tenantId: string): void {
    this.logoFailedIds.update(ids => new Set(ids).add(tenantId));
  }

  private load(): void {
    this.isLoading.set(true);
    this.loadFailed.set(false);
    this.membershipsService.getMyWallets().subscribe({
      next: wallets => {
        this.wallets.set(wallets);
        this.isLoading.set(false);
        this.loadRecentActivity(wallets);
      },
      error: () => {
        this.isLoading.set(false);
        this.loadFailed.set(true);
      },
    });
  }

  // Merges each joined business's own last few transactions into one cross-business feed —
  // WalletAppService.GetMyTransactionHistoryAsync is tenant-scoped (no cross-tenant equivalent exists),
  // so this is a bounded fan-out (see RECENT_ACTIVITY_WALLET_FANOUT), not a new backend endpoint. Each
  // request's own failure is swallowed to `of([])` rather than failing the whole feed — one business
  // being briefly unreachable shouldn't blank out every other business's activity.
  private loadRecentActivity(wallets: PointsWalletDto[]): void {
    const walletsToQuery = wallets.filter((w): w is PointsWalletDto & { tenantId: string } => !!w.tenantId).slice(0, RECENT_ACTIVITY_WALLET_FANOUT);

    if (walletsToQuery.length === 0) {
      this.recentActivityLoading.set(false);
      return;
    }

    this.recentActivityLoading.set(true);
    const requests = walletsToQuery.map(wallet =>
      this.walletService
        .getMyTransactionHistory(wallet.tenantId, {
          type: null,
          sorting: 'creationTime desc',
          skipCount: 0,
          maxResultCount: RECENT_ACTIVITY_PER_WALLET,
        })
        .pipe(
          map(result =>
            (result.items ?? []).map(
              (transaction): RecentActivityItem => ({
                transaction,
                businessName: wallet.businessName ?? '',
              }),
            ),
          ),
          catchError(() => of<RecentActivityItem[]>([])),
        ),
    );

    forkJoin(requests).subscribe(resultsPerWallet => {
      const merged = resultsPerWallet
        .flat()
        .sort((a, b) => new Date(b.transaction.creationTime ?? 0).getTime() - new Date(a.transaction.creationTime ?? 0).getTime())
        .slice(0, RECENT_ACTIVITY_PREVIEW_COUNT);
      this.recentActivity.set(merged);
      this.recentActivityLoading.set(false);
    });
  }
}
