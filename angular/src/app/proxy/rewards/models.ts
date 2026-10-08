import type { AuditedEntityDto, FullAuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { CouponStatus } from './coupon-status.enum';
import type { RewardType } from './reward-type.enum';

export interface CouponAuditFilterDto extends PagedAndSortedResultRequestDto {
  status?: CouponStatus | null;
  branchId?: string | null;
  membershipId?: string | null;
}

export interface CouponDto extends AuditedEntityDto<string> {
  rewardId?: string;
  rewardNameAr?: string | null;
  rewardNameEn?: string | null;
  membershipId?: string;
  tenantId?: string | null;
  code?: string;
  status?: CouponStatus;
  pointsCost?: number;
  issuedAt?: string;
  reservationExpiresAt?: string | null;
  redeemedAt?: string | null;
  redeemedByEmployeeId?: string | null;
  redeemedBranchId?: string | null;
  rejectionReason?: string | null;
}

export interface CouponExcelDownloadDto {
  downloadToken?: string;
  status?: CouponStatus | null;
  branchId?: string | null;
  sorting?: string | null;
}

export interface CreateUpdateRewardDto {
  nameAr: string;
  nameEn: string;
  type: RewardType;
  pointsCost?: number;
  stockRemaining?: number | null;
  validFrom?: string | null;
  validTo?: string | null;
  imageBlobName?: string | null;
  approvalThresholdPoints?: number | null;
}

export interface RedeemRewardDto {
  tenantId: string;
  rewardId: string;
}

export interface RewardDto extends FullAuditedEntityDto<string> {
  tenantId?: string | null;
  nameAr?: string;
  nameEn?: string;
  type?: RewardType;
  pointsCost?: number;
  stockRemaining?: number | null;
  validFrom?: string | null;
  validTo?: string | null;
  imageBlobName?: string | null;
  approvalThresholdPoints?: number | null;
}

// Deliberately separate from RewardDto, which the Business Portal's own reward CRUD also uses —
// businessName/availableBalance/canAfford only make sense once a reward is seen in the context of
// "across every business I belong to".
export interface CustomerRewardDto {
  id: string;
  tenantId: string;
  nameAr: string;
  nameEn: string;
  type: RewardType;
  pointsCost: number;
  stockRemaining?: number | null;
  imageBlobName?: string | null;
  businessName: string;
  availableBalance: number;
  canAfford: boolean;
  pointsNeeded: number;
}

export interface CustomerRewardListDto {
  items: CustomerRewardDto[];
}
