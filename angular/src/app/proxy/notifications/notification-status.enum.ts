import { mapEnumToOptions } from '@abp/ng.core';

export enum NotificationStatus {
  Queued = 0,
  Sent = 1,
  Failed = 2,
}

export const notificationStatusOptions = mapEnumToOptions(NotificationStatus);
