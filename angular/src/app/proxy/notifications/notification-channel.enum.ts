import { mapEnumToOptions } from '@abp/ng.core';

export enum NotificationChannel {
  Push = 0,
  Email = 1,
  Sms = 2,
  InApp = 3,
}

export const notificationChannelOptions = mapEnumToOptions(NotificationChannel);
