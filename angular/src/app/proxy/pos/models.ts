import type { PointRuleType } from '../wallets/point-rule-type.enum';

export interface AwardPointsByCustomerIdDto {
  purchaseAmount?: number | null;
}

export interface AwardPointsByQrDto {
  qrToken: string;
  purchaseAmount?: number | null;
}

export interface AwardPointsResultDto {
  transactionId?: string;
  pointsAwarded?: number;
  newBalance?: number;
  newTierId?: string | null;
  newTierName?: string | null;
}

export interface ConfirmRedemptionDto {
  code: string;
  branchId?: string | null;
}

export interface CustomerLookupResultDto {
  customerId?: string;
  membershipId?: string;
  walletId?: string;
  balance?: number;
  firstName?: string | null;
  lastName?: string | null;
}

export interface ManualAdjustDto {
  customerId: string;
  points: number;
  reason?: string | null;
}

export interface PhoneLookupDto {
  phoneNumber: string;
}

export interface PointsPreviewDto {
  basePoints?: number;
  ruleType?: PointRuleType;
  pointsPerUnit?: number;
  tierMultiplier?: number;
  tierName?: string | null;
  campaignMultiplier?: number;
  campaignName?: string | null;
  campaignBonusPoints?: number;
  bonusCampaignName?: string | null;
  totalPoints?: number;
}

export interface PreviewPointsDto {
  purchaseAmount?: number | null;
}

export interface RedemptionConfirmationDto {
  couponId?: string;
  rewardNameAr?: string | null;
  rewardNameEn?: string | null;
  redeemedAt?: string;
}
