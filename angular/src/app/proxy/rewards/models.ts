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
  issuedAt?: string;
  redeemedAt?: string | null;
  redeemedByEmployeeId?: string | null;
  redeemedBranchId?: string | null;
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
