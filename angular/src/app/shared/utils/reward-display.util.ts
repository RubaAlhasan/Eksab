import type { RewardDto } from '../../proxy/rewards/models';
import { RewardType } from '../../proxy/rewards/reward-type.enum';

// Same real threshold the Business Portal's own Rewards page (business-rewards.component.ts) and
// `ReportsAppService.GetDashboardHomeAsync` use server-side for their own low-stock alert.
const LOW_STOCK_THRESHOLD = 10;

export type RewardStatus = 'active' | 'scheduled' | 'expired';

/** `Reward` has no `IsActive`/`Status` column — derived client-side from `ValidFrom`/`ValidTo`, same
 *  logic `business-rewards.component.ts` already established for the staff-facing Rewards page. */
export function rewardStatus(reward: RewardDto): RewardStatus {
  const now = Date.now();
  if (reward.validTo && new Date(reward.validTo).getTime() < now) return 'expired';
  if (reward.validFrom && new Date(reward.validFrom).getTime() > now) return 'scheduled';
  return 'active';
}

export function isLowStock(reward: RewardDto, threshold = LOW_STOCK_THRESHOLD): boolean {
  return reward.stockRemaining != null && reward.stockRemaining < threshold;
}

export function isOutOfStock(reward: RewardDto): boolean {
  return reward.stockRemaining != null && reward.stockRemaining <= 0;
}

/** No reward image exists anywhere in this app yet (see business-rewards.component.ts's own comment) —
 *  a deterministic per-type emoji stands in for a per-item image. */
export function rewardTypeEmoji(type: RewardType | undefined): string {
  switch (type) {
    case RewardType.FreeProduct:
      return '🎁';
    case RewardType.GiftCard:
      return '💳';
    default:
      return '🏷️';
  }
}
