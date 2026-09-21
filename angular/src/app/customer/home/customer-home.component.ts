import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ConfigStateService, LocalizationPipe } from '@abp/ng.core';
import { environment } from '../../../environments/environment';
import { MembershipsService } from '../../proxy/controllers/memberships.service';
import { CustomerProfileService } from '../../proxy/controllers/customer-profile.service';
import { CustomerCampaignService } from '../../proxy/controllers/customer-campaign.service';
import { CustomerBusinessService } from '../../proxy/controllers/customer-business.service';
import { NotificationHubService } from '../../shared/services/notification-hub.service';
import type { PointsWalletDto } from '../../proxy/wallets/models';
import type { CustomerCampaignDto } from '../../proxy/campaigns/models';
import type { CustomerBusinessDto } from '../../proxy/businesses/models';
import { LoadingSpinnerComponent } from '../../shared/components/loading-spinner/loading-spinner.component';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../shared/components/error-state/error-state.component';
import { campaignTypeEmoji, campaignTypeLabelKey } from '../../shared/utils/campaign-display.util';

const DISCOVER_CANDIDATE_COUNT = 8;
const DISCOVER_PREVIEW_COUNT = 4;
const CAMPAIGN_PREVIEW_COUNT = 6;

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
    LoadingSpinnerComponent,
    EmptyStateComponent,
    ErrorStateComponent,
  ],
})
export class CustomerHomeComponent implements OnInit {
  private readonly membershipsService = inject(MembershipsService);
  private readonly customerProfileService = inject(CustomerProfileService);
  private readonly customerCampaignService = inject(CustomerCampaignService);
  private readonly customerBusinessService = inject(CustomerBusinessService);
  private readonly configState = inject(ConfigStateService);
  protected readonly hub = inject(NotificationHubService);

  protected readonly isLoading = signal(true);
  protected readonly loadFailed = signal(false);
  protected readonly wallets = signal<PointsWalletDto[]>([]);
  protected readonly displayName = signal<string | null>(null);

  protected readonly campaigns = signal<CustomerCampaignDto[]>([]);
  protected readonly discoverCandidates = signal<CustomerBusinessDto[]>([]);

  protected readonly campaignTypeEmoji = campaignTypeEmoji;
  protected readonly campaignTypeLabelKey = campaignTypeLabelKey;

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
      },
      error: () => {
        this.isLoading.set(false);
        this.loadFailed.set(true);
      },
    });
  }
}
