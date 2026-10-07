import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { LocalizedNamePipe } from '../../shared/pipes/localized-name.pipe';
import { CouponsService } from '../../proxy/controllers/coupons.service';
import { MembershipsService } from '../../proxy/controllers/memberships.service';
import type { RewardDto } from '../../proxy/rewards/models';
import { rewardStatus, rewardTypeEmoji } from '../../shared/utils/reward-display.util';
import { CustomerRewardCacheService } from './customer-reward-cache.service';

/**
 * No customer-facing by-id reward endpoint exists — `reward` is read from `CustomerRewardCacheService`
 * (populated by `CustomerRewardsCatalogComponent`'s own catalog fetch), not a network call. On a hard
 * refresh of this deep link (cache empty — it's a plain in-memory singleton, lost on reload) this
 * bounces back to the catalog for the same `tenantId`, which is still fetchable.
 */
@Component({
  selector: 'app-customer-reward-details',
  templateUrl: './customer-reward-details.component.html',
  styleUrls: ['./customer-reward-details.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, LocalizationPipe, LocalizedNamePipe],
})
export class CustomerRewardDetailsComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly couponsService = inject(CouponsService);
  private readonly membershipsService = inject(MembershipsService);
  private readonly rewardCache = inject(CustomerRewardCacheService);

  // See customer-points.component.ts's identical comment on why these aren't snapshot field
  // initializers.
  protected tenantId = '';
  private rewardId = '';

  protected readonly reward = signal<RewardDto | undefined>(undefined);
  protected readonly balance = signal<number | null>(null);
  // See PointsWalletDto.availableBalance's own comment — what's actually spendable, not the raw
  // balance, since some of it may be held against a Pending redemption started elsewhere.
  protected readonly availableBalance = signal<number | null>(null);
  protected readonly reserved = signal(0);
  protected readonly confirmOpen = signal(false);
  protected readonly isRedeeming = signal(false);

  protected readonly rewardTypeEmoji = rewardTypeEmoji;
  protected readonly status = computed(() => (this.reward() ? rewardStatus(this.reward()!) : 'active'));

  protected readonly canAfford = computed(() => {
    const reward = this.reward();
    const available = this.availableBalance();
    return !reward || available == null || reward.pointsCost == null || available >= reward.pointsCost;
  });

  ngOnInit(): void {
    this.route.paramMap.subscribe(params => {
      const tenantId = params.get('tenantId');
      const rewardId = params.get('rewardId');
      if (!tenantId || !rewardId) return;
      this.tenantId = tenantId;
      this.rewardId = rewardId;
      this.confirmOpen.set(false);

      const cached = this.rewardCache.get(rewardId);
      if (!cached) {
        // No back-fill call to make — the catalog is the only place this reward could have come from.
        void this.router.navigate(['/customer/wallet', tenantId, 'rewards']);
        return;
      }
      this.reward.set(cached);

      this.membershipsService.getMyWallets().subscribe({
        next: wallets => {
          const wallet = wallets.find(w => w.tenantId === tenantId);
          this.balance.set(wallet?.balance ?? null);
          this.availableBalance.set(wallet?.availableBalance ?? wallet?.balance ?? null);
          this.reserved.set(wallet?.reserved ?? 0);
        },
        error: () => undefined,
      });
    });
  }

  protected openConfirm(): void {
    if (!this.canAfford() || this.isRedeeming()) return;
    this.confirmOpen.set(true);
  }

  protected cancelConfirm(): void {
    this.confirmOpen.set(false);
  }

  protected confirmRedeem(): void {
    if (this.isRedeeming()) return;
    this.isRedeeming.set(true);
    this.couponsService.redeem({ tenantId: this.tenantId, rewardId: this.rewardId }).subscribe({
      next: coupon => {
        this.isRedeeming.set(false);
        void this.router.navigate(['/customer/redeem', this.tenantId, coupon.id]);
      },
      // The interceptor already surfaces the server's own message (insufficient points, out of stock,
      // reward no longer available) — same idiom as business-redemption.component.ts's performLookup.
      error: () => {
        this.isRedeeming.set(false);
        this.confirmOpen.set(false);
      },
    });
  }
}
