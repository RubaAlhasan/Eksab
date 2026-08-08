import type { AuditedEntityDto, FullAuditedEntityDto } from '@abp/ng.core';
import type { ReferralStatus } from './referral-status.enum';

export interface AchievementAwardDto extends AuditedEntityDto<string> {
  membershipId?: string;
  achievementId?: string;
  awardedAt?: string;
}

export interface AchievementDto extends FullAuditedEntityDto<string> {
  tenantId?: string | null;
  name?: string;
  criteriaJson?: string | null;
}

export interface AwardAchievementDto {
  membershipId: string;
  achievementId: string;
}

export interface CreateUpdateAchievementDto {
  name: string;
  criteriaJson?: string | null;
}

export interface FollowDto extends AuditedEntityDto<string> {
  customerId?: string;
  tenantId?: string | null;
  followedAt?: string;
}

export interface ReferralCodeDto {
  code?: string;
}

export interface ReferralDto extends AuditedEntityDto<string> {
  referrerMembershipId?: string;
  refereeCustomerId?: string;
  tenantId?: string | null;
  status?: ReferralStatus;
}
