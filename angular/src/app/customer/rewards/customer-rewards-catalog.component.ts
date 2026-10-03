import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { CouponsService } from '../../proxy/controllers/coupons.service';
import { MembershipsService } from '../../proxy/controllers/memberships.service';
import type { RewardDto } from '../../proxy/rewards/models';
import { SkeletonListComponent } from '../../shared/components/skeleton-list/skeleton-list.component';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../shared/components/error-state/error-state.component';
import { PaginationComponent } from '../../shared/components/pagination/pagination.component';
import { StatusBadgeComponent, StatusBadgeVariant } from '../../shared/components/status-badge/status-badge.component';
import { isLowStock, isOutOfStock, rewardStatus, rewardTypeEmoji, RewardStatus } from '../../shared/utils/reward-display.util';
import { CustomerRewardCacheService } from './customer-reward-cache.service';

@Component({
  selector: 'app-customer-rewards-catalog',
  templateUrl: './customer-rewards-catalog.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink,
    LocalizationPipe,
    SkeletonListComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    PaginationComponent,
    StatusBadgeComponent,
  ],
})
export class CustomerRewardsCatalogComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly couponsService = inject(CouponsService);
  private readonly membershipsService = inject(MembershipsService);
  private readonly rewardCache = inject(CustomerRewardCacheService);

  private readonly pageSize = 10;
  // See customer-points.component.ts's identical comment on why this isn't a snapshot field initializer.
  protected tenantId = '';

  protected readonly rewards = signal<RewardDto[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly balance = signal<number | null>(null);
  // Null until loadBalance resolves; separate from `balance` because affordability must check what's
  // actually spendable, not the raw balance — see PointsWalletDto.availableBalance's own comment.
  protected readonly availableBalance = signal<number | null>(null);
  protected readonly reserved = signal(0);
  protected readonly isLoading = signal(true);
  protected readonly loadFailed = signal(false);
  protected readonly pageIndex = signal(0);
  protected readonly totalPages = computed(() => Math.max(1, Math.ceil(this.totalCount() / this.pageSize)));

  protected readonly rewardTypeEmoji = rewardTypeEmoji;
  protected readonly isLowStock = isLowStock;
  protected readonly isOutOfStock = isOutOfStock;

  ngOnInit(): void {
    this.route.paramMap.subscribe(params => {
      const tenantId = params.get('tenantId');
      if (!tenantId) return;
      this.tenantId = tenantId;
      this.pageIndex.set(0);
      this.loadBalance(tenantId);
      this.load(tenantId);
    });
  }

  protected retry(): void {
    if (this.tenantId) this.load(this.tenantId);
  }

  protected goToPage(index: number): void {
    if (index < 0 || index >= this.totalPages() || !this.tenantId) return;
    this.pageIndex.set(index);
    this.load(this.tenantId);
  }

  protected canAfford(reward: RewardDto): boolean {
    const available = this.availableBalance();
    return available == null || reward.pointsCost == null || available >= reward.pointsCost;
  }

  protected statusLabelKey(status: RewardStatus): string {
    switch (status) {
      case 'expired':
        return '::Wallet:Rewards:StatusExpired';
      case 'scheduled':
        return '::Wallet:Rewards:StatusScheduled';
      default:
        return '::Wallet:Rewards:StatusActive';
    }
  }

  protected statusVariant(status: RewardStatus): StatusBadgeVariant {
    switch (status) {
      case 'expired':
        return 'neutral';
      case 'scheduled':
        return 'info';
      default:
        return 'success';
    }
  }

  protected status(reward: RewardDto): RewardStatus {
    return rewardStatus(reward);
  }

  private loadBalance(tenantId: string): void {
    this.membershipsService.getMyWallets().subscribe({
      next: wallets => {
        const wallet = wallets.find(w => w.tenantId === tenantId);
        this.balance.set(wallet?.balance ?? null);
        this.availableBalance.set(wallet?.availableBalance ?? wallet?.balance ?? null);
        this.reserved.set(wallet?.reserved ?? 0);
      },
      error: () => undefined,
    });
  }

  private load(tenantId: string): void {
    this.isLoading.set(true);
    this.loadFailed.set(false);

    this.couponsService
      .getCatalog(tenantId, {
        sorting: 'creationTime desc',
        skipCount: this.pageIndex() * this.pageSize,
        maxResultCount: this.pageSize,
      })
      .subscribe({
        next: result => {
          const items = result.items ?? [];
          this.rewards.set(items);
          this.totalCount.set(result.totalCount ?? 0);
          this.rewardCache.setMany(items);
          this.isLoading.set(false);
        },
        error: () => {
          this.isLoading.set(false);
          this.loadFailed.set(true);
        },
      });
  }
}
