import { Injectable, signal } from '@angular/core';
import type { RewardDto } from '../../proxy/rewards/models';

/**
 * No customer-facing by-id reward endpoint exists — `CouponsService.getCatalog` (paged) is the only
 * customer-facing reward read. This holds whatever the catalog page has already fetched so the details
 * page can render instantly instead of re-fetching (there's nothing to re-fetch from anyway).
 *
 * A plain in-memory cache, not persisted: on a hard refresh of a deep-linked details URL, the cache is
 * empty and `CustomerRewardDetailsComponent` bounces back to the catalog for that same business (whose
 * `tenantId` the URL still carries) rather than erroring.
 */
@Injectable({ providedIn: 'root' })
export class CustomerRewardCacheService {
  private readonly rewardsById = signal<Map<string, RewardDto>>(new Map());

  setMany(rewards: RewardDto[]): void {
    this.rewardsById.update(current => {
      const next = new Map(current);
      for (const reward of rewards) {
        if (reward.id) next.set(reward.id, reward);
      }
      return next;
    });
  }

  get(id: string): RewardDto | undefined {
    return this.rewardsById().get(id);
  }
}
