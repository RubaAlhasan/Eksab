import type { EntityDto, PagedResultRequestDto } from '@abp/ng.core';
import type { NotificationTargetType } from './notification-target-type.enum';
import type { UserNotificationType } from './user-notification-type.enum';

export interface UserNotificationDto extends EntityDto<string> {
  type: UserNotificationType;
  title: string;
  message: string;
  category?: string | null;
  data?: string | null;
  isRead: boolean;
  readAt?: string | null;
  creationTime: string;
}

export interface UserNotificationListFilterDto extends PagedResultRequestDto {
  isRead?: boolean | null;
}

export interface SendUserNotificationDto {
  targetType: NotificationTargetType;
  userId?: string | null;
  tenantId?: string | null;
  type: UserNotificationType;
  title: string;
  message: string;
  category?: string | null;
}
