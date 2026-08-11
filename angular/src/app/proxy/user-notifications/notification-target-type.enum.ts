import { mapEnumToOptions } from '@abp/ng.core';

export enum NotificationTargetType {
  User = 0,
  Tenant = 1,
  Broadcast = 2,
}

export const notificationTargetTypeOptions = mapEnumToOptions(NotificationTargetType);
