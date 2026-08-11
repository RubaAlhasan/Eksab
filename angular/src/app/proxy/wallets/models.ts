import type { PointRuleType } from './point-rule-type.enum';
import type { AuditedEntityDto, EntityDto } from '@abp/ng.core';
import type { PointsTransactionType } from './points-transaction-type.enum';
import type { PointsTransactionSource } from './points-transaction-source.enum';

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

export interface PointsTransactionDto extends EntityDto<string> {
  walletId?: string;
  type?: PointsTransactionType;
  points?: number;
  source?: PointsTransactionSource;
  referenceId?: string | null;
  expiresAt?: string | null;
  createdByEmployeeId?: string | null;
  reason?: string | null;
  tierMultiplierSnapshot?: number | null;
  creationTime?: string;
}

export interface PointsWalletDto extends AuditedEntityDto<string> {
  membershipId?: string;
  tenantId?: string | null;
  balance?: number;
  lifetimeEarned?: number;
  lifetimeRedeemed?: number;
  currentTierId?: string | null;
  currentTierName?: string | null;
}

export interface TierDto extends AuditedEntityDto<string> {
  tenantId?: string | null;
  name?: string;
  minLifetimePoints?: number;
  multiplier?: number;
}
