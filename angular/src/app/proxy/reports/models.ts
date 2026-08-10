import type { PagedAndSortedResultRequestDto, EntityDto } from '@abp/ng.core';
import type { NotificationChannel } from '../notifications/notification-channel.enum';
import type { PointsTransactionType } from '../wallets/points-transaction-type.enum';
import type { PointsTransactionSource } from '../wallets/points-transaction-source.enum';

export interface BranchComparisonDto {
  branchId?: string;
  branchName?: string;
  redemptionCount?: number;
}

export interface CampaignPerformanceDto {
  campaignId?: string;
  notificationsSent?: number;
  notificationsQueued?: number;
  notificationsFailed?: number;
  bonusPointsAwarded?: number;
  membershipsRewarded?: number;
}

export interface CustomerSegmentReportDto {
  new?: number;
  active?: number;
  atRisk?: number;
  churned?: number;
}

export interface DashboardHomeDto {
  activeMemberCount?: number;
  pointsIssuedLast30Days?: number;
  pointsRedeemedLast30Days?: number;
  activeCampaignCount?: number;
  lowStockRewards?: LowStockRewardDto[];
}

export interface LowStockRewardDto {
  id?: string;
  nameAr?: string;
  nameEn?: string;
  stockRemaining?: number;
}

export interface MemberGrowthPointDto {
  date?: string;
  newMembers?: number;
}

export interface NotificationDeliveryRateDto {
  channel?: NotificationChannel;
  sent?: number;
  failed?: number;
  queued?: number;
  deliveryRate?: number;
}

export interface RedemptionRateReportDto {
  earnedPoints?: number;
  redeemedPoints?: number;
  redemptionRate?: number;
}

export interface ReportPeriodDto {
  from: string;
  to: string;
}

export interface TierDistributionDto {
  tierId?: string | null;
  tierName?: string | null;
  memberCount?: number;
}

export interface TopCustomerDto {
  membershipId?: string;
  customerId?: string;
  lifetimeEarned?: number;
  firstName?: string | null;
  lastName?: string | null;
}

export interface TransactionsExcelDownloadDto {
  downloadToken?: string;
  from?: string;
  to?: string;
}

export interface TransactionFilterDto extends PagedAndSortedResultRequestDto {
  type?: PointsTransactionType | null;
  branchId?: string | null;
  staffId?: string | null;
  from?: string | null;
  to?: string | null;
}

export interface TransactionListItemDto extends EntityDto<string> {
  customerId?: string | null;
  customerFirstName?: string | null;
  customerLastName?: string | null;
  type?: PointsTransactionType;
  points?: number;
  source?: PointsTransactionSource;
  branchId?: string | null;
  staffId?: string | null;
  creationTime?: string;
}
