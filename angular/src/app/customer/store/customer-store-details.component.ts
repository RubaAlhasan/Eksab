import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { RouterLink, ActivatedRoute } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { LocalizedNamePipe } from '../../shared/pipes/localized-name.pipe';
import { environment } from '../../../environments/environment';
import { CustomerBusinessService } from '../../proxy/controllers/customer-business.service';
import { FollowsService } from '../../proxy/controllers/follows.service';
import { MembershipsService } from '../../proxy/controllers/memberships.service';
import { CouponsService } from '../../proxy/controllers/coupons.service';
import { CustomerSmartOffersService } from '../../proxy/controllers/customer-smart-offers.service';
import { CustomerCampaignService } from '../../proxy/controllers/customer-campaign.service';
import type { CustomerBusinessBranchDto, CustomerBusinessDto, CustomerEarnRuleDto } from '../../proxy/businesses/models';
import { PointRuleType } from '../../proxy/wallets/point-rule-type.enum';
import { Currency } from '../../proxy/shared/currency.enum';
import type { RewardDto } from '../../proxy/rewards/models';
import type { CustomerSmartOfferDto } from '../../proxy/smart-offers/models';
import type { CustomerCampaignDto } from '../../proxy/campaigns/models';
import { SkeletonListComponent } from '../../shared/components/skeleton-list/skeleton-list.component';
import { displayUrl, toExternalHref } from '../../shared/utils/contact-display.util';
import { ErrorStateComponent } from '../../shared/components/error-state/error-state.component';
import { rewardTypeEmoji } from '../../shared/utils/reward-display.util';
import { campaignTypeEmoji, campaignTypeLabelKey } from '../../shared/utils/campaign-display.util';
import { PaginationComponent } from '../../shared/components/pagination/pagination.component';
import { SmartDealCardComponent } from '../../shared/components/smart-deal-card/smart-deal-card.component';

type StoreTab = 'about' | 'offers' | 'rewards';

// Smart deals from this business, paged in the browser: the list is already loaded in full for the Offers tab.
const SMART_DEALS_PAGE_SIZE = 6;

/**
 * Store Details — folds the prototype's separate join-store.html into this same page (a "Join" button
 * with an optional referral-code field, inline) rather than a distinct route: the only reason
 * join-store.html existed as its own screen was its QR-scan-to-join tab, and there's no backend support
 * for scanning a branch QR to join (MembershipAppService.JoinAsync only takes TenantId + optional
 * ReferralCode — confirmed by reading it) — so once that tab is dropped, a separate screen would just be
 * this same form with extra navigation.
 *
 * No star rating anywhere — `CustomerBusinessDto` has no such field (confirmed by reading it); the
 * prototype's rating is fake data, not translated here. `BranchCount` stays the header's own aggregate
 * stat, but About now also lists each branch by name with its address, opening hours (as the business
 * wrote them — free text, not structured), a maps link when it has been placed on a map, and its phone.
 */
@Component({
  selector: 'app-customer-store-details',
  templateUrl: './customer-store-details.component.html',
  styleUrls: ['./customer-store-details.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink,
    DatePipe,
    DecimalPipe,
    LocalizationPipe,
    LocalizedNamePipe,
    SkeletonListComponent,
    ErrorStateComponent,
    PaginationComponent,
    SmartDealCardComponent,
  ],
})
export class CustomerStoreDetailsComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly customerBusinessService = inject(CustomerBusinessService);
  private readonly followsService = inject(FollowsService);
  private readonly membershipsService = inject(MembershipsService);
  private readonly couponsService = inject(CouponsService);
  private readonly customerCampaignService = inject(CustomerCampaignService);
  private readonly customerSmartOffersService = inject(CustomerSmartOffersService);

  // See customer-points.component.ts's identical comment on why this isn't a snapshot field
  // initializer.
  private tenantId = '';

  protected readonly isLoading = signal(true);
  protected readonly loadFailed = signal(false);
  protected readonly business = signal<CustomerBusinessDto | null>(null);
  protected readonly isMember = signal(false);
  protected readonly toExternalHref = toExternalHref;
  protected readonly displayUrl = displayUrl;
  protected readonly isFollowing = signal(false);
  protected readonly isFollowBusy = signal(false);
  protected readonly logoFailed = signal(false);

  protected readonly activeTab = signal<StoreTab>('about');
  protected readonly previewRewards = signal<RewardDto[]>([]);
  protected readonly offers = signal<CustomerCampaignDto[]>([]);
  // How the business awards points. Empty when it has none, and the section is then left out.
  protected readonly earnRules = signal<CustomerEarnRuleDto[]>([]);
  protected readonly PointRuleType = PointRuleType;

  protected currencyCode(currency: Currency | null | undefined): string {
    return currency === Currency.Syp ? 'SYP' : 'USD';
  }
  protected readonly offersLoaded = signal(false);
  protected readonly smartDeals = signal<CustomerSmartOfferDto[]>([]);
  protected readonly smartDealsLoaded = signal(false);
  protected readonly smartDealPageIndex = signal(0);
  protected readonly smartDealPages = computed(() => Math.max(1, Math.ceil(this.smartDeals().length / SMART_DEALS_PAGE_SIZE)));
  protected readonly pagedSmartDeals = computed(() => {
    const start = this.smartDealPageIndex() * SMART_DEALS_PAGE_SIZE;
    return this.smartDeals().slice(start, start + SMART_DEALS_PAGE_SIZE);
  });

  protected readonly showJoinForm = signal(false);
  protected readonly referralCode = signal('');
  protected readonly isJoining = signal(false);

  protected readonly rewardTypeEmoji = rewardTypeEmoji;
  protected readonly campaignTypeEmoji = campaignTypeEmoji;
  protected readonly campaignTypeLabelKey = campaignTypeLabelKey;

  protected readonly logoUrl = computed(() => {
    const business = this.business();
    if (!business?.hasLogo || this.logoFailed()) return null;
    return `${environment.apis.default.url}/api/app/business/${business.businessProfileId}/logo?v=${business.logoBlobName ?? ''}`;
  });

  // Every branch the business lists, with whatever it has entered for each one. A branch with no address, hours or
  // phone still shows its name, so a customer can see the business has more than one place.
  protected readonly branches = computed(() => this.business()?.branches ?? []);

  // Opens the branch in the device's maps app. Null when the business has not placed the branch on a map.
  protected mapLink(branch: CustomerBusinessBranchDto): string | null {
    if (branch.latitude == null || branch.longitude == null) return null;
    return `https://www.google.com/maps/search/?api=1&query=${branch.latitude},${branch.longitude}`;
  }

  ngOnInit(): void {
    this.route.paramMap.subscribe(params => {
      const tenantId = params.get('tenantId');
      if (!tenantId) return;
      this.tenantId = tenantId;
      this.activeTab.set('about');
      this.showJoinForm.set(false);
      this.load(tenantId);
    });
  }

  protected retry(): void {
    if (this.tenantId) this.load(this.tenantId);
  }

  protected goToSmartDealPage(index: number): void {
    if (index < 0 || index >= this.smartDealPages()) return;
    this.smartDealPageIndex.set(index);
  }

  protected selectTab(tab: StoreTab): void {
    this.activeTab.set(tab);
    if (tab === 'rewards' && this.previewRewards().length === 0) {
      this.couponsService.getCatalog(this.tenantId, { maxResultCount: 5, skipCount: 0, sorting: 'creationTime desc' }).subscribe({
        next: result => this.previewRewards.set(result.items ?? []),
        error: () => undefined,
      });
    }
    if (tab === 'offers' && !this.smartDealsLoaded()) {
      this.customerSmartOffersService.getOffers(this.tenantId).subscribe({
        next: result => {
          this.smartDeals.set(result.items ?? []);
          this.smartDealsLoaded.set(true);
        },
        error: () => undefined,
      });
    }
    if (tab === 'offers' && !this.offersLoaded()) {
      this.customerCampaignService.getForBusiness(this.tenantId).subscribe({
        next: offers => {
          this.offers.set(offers);
          this.offersLoaded.set(true);
        },
        error: () => undefined,
      });
    }
  }

  protected toggleFollow(): void {
    if (this.isFollowBusy()) return;
    this.isFollowBusy.set(true);
    const request = this.isFollowing()
      ? this.followsService.unfollow(this.tenantId)
      : this.followsService.follow(this.tenantId);
    request.subscribe({
      next: () => {
        this.isFollowBusy.set(false);
        this.isFollowing.set(!this.isFollowing());
      },
      error: () => this.isFollowBusy.set(false),
    });
  }

  protected openJoinForm(): void {
    this.showJoinForm.set(true);
  }

  protected cancelJoinForm(): void {
    this.showJoinForm.set(false);
    this.referralCode.set('');
  }

  protected onReferralCodeInput(event: Event): void {
    this.referralCode.set((event.target as HTMLInputElement).value);
  }

  protected confirmJoin(): void {
    if (this.isJoining()) return;
    this.isJoining.set(true);
    this.membershipsService.join({ tenantId: this.tenantId, referralCode: this.referralCode() || null }).subscribe({
      // Stay on the business page: About, phone numbers, offers and rewards are the reason someone opened it,
      // and the points page has no link back here. The header switches to "View My Points" instead.
      next: () => {
        this.isJoining.set(false);
        this.isMember.set(true);
        this.showJoinForm.set(false);
        this.referralCode.set('');
      },
      // The interceptor already surfaces the server's own message — same idiom used elsewhere in this
      // app for expected, user-facing failures.
      error: () => this.isJoining.set(false),
    });
  }

  private load(tenantId: string): void {
    this.isLoading.set(true);
    this.loadFailed.set(false);
    this.logoFailed.set(false);
    this.previewRewards.set([]);
    this.offers.set([]);
    this.offersLoaded.set(false);
    this.smartDeals.set([]);
    this.smartDealsLoaded.set(false);
    this.earnRules.set([]);
    // Angular reuses this component across a same-route, different-store navigation (see the field comment on
    // tenantId above). Without resetting these, the header could show "View My Points"/"Following" for a store the
    // customer has not joined or followed, left over from whichever store was open before, until these two
    // independent requests below happen to resolve.
    this.isMember.set(false);
    this.isFollowing.set(false);

    this.customerBusinessService.get(tenantId).subscribe({
      next: business => {
        this.business.set(business);
        this.isLoading.set(false);
      },
      error: () => {
        this.isLoading.set(false);
        this.loadFailed.set(true);
      },
    });

    this.customerBusinessService.getEarnRules(tenantId).subscribe({
      next: rules => this.earnRules.set(rules),
      error: () => this.earnRules.set([]),
    });

    this.membershipsService.getMyWallets().subscribe({
      next: wallets => this.isMember.set(wallets.some(w => w.tenantId === tenantId)),
      error: () => undefined,
    });

    this.followsService.getMyFollows().subscribe({
      next: follows => this.isFollowing.set(follows.some(f => f.tenantId === tenantId)),
      error: () => undefined,
    });
  }
}
