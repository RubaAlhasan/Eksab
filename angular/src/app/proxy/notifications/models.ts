import type { AuditedEntityDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type { NotificationChannel } from './notification-channel.enum';
import type { NotificationStatus } from './notification-status.enum';

export interface NotificationDto extends AuditedEntityDto<string> {
  membershipId?: string | null;
  tenantId?: string | null;
  campaignId?: string | null;
  channel?: NotificationChannel;
  title?: string;
  body?: string;
  status?: NotificationStatus;
  sentAt?: string | null;
}

export interface NotificationListFilterDto extends PagedAndSortedResultRequestDto {
  campaignId?: string | null;
  status?: NotificationStatus | null;
  channel?: NotificationChannel | null;
}

export interface NotificationQuotaUsageDto {
  sentToday?: number;
  dailyLimit?: number;
}

export interface SendNotificationDto {
  membershipId: string;
  channel: NotificationChannel;
  title: string;
  body: string;
}
