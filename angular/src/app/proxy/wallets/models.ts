import type { PointRuleType } from './point-rule-type.enum';
import type { AuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { PointsTransactionType } from './points-transaction-type.enum';
import type { PointsTransactionSource } from './points-transaction-source.enum';

// Hand-added field — see the file comment on WalletService.getMyTransactionHistory in
// ../controllers/wallet.service.ts for why (GetMyTransactionHistoryAsync grew an optional `type`
// filter, never regenerated into this proxy).
export interface GetMyTransactionHistoryInput extends PagedAndSortedResultRequestDto {
  type?: PointsTransactionType | null;
}

export interface CreateUpdatePointRuleDto {
  ruleType: PointRuleType;
  pointsPerUnit: number;
}

export interface CreateUpdateTierDto {
  name: string;
  minLifetimePoints: number;
  multiplier: number;
}

export interface PointRuleDto extends AuditedEntityDto<string> {
  tenantId?: string | null;
  ruleType?: PointRuleType;
  pointsPerUnit?: number;
}

export interface PointsWalletDto extends AuditedEntityDto<string> {
  membershipId?: string;
  tenantId?: string | null;
  balance?: number;
  // Points held against a Pending redemption elsewhere — see PointsWallet.Reserved's own comment
  // (backend) for why this is separate from `balance` rather than already subtracted from it.
  reserved?: number;
  availableBalance?: number;
  lifetimeEarned?: number;
  lifetimeRedeemed?: number;
  currentTierId?: string | null;
  currentTierName?: string | null;
  businessName?: string | null;
}

export interface TierDto extends AuditedEntityDto<string> {
  tenantId?: string | null;
  name?: string;
  minLifetimePoints?: number;
  multiplier?: number;
}
