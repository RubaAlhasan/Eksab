import { mapEnumToOptions } from '@abp/ng.core';

export enum UserNotificationType {
  Info = 0,
  Success = 1,
  Warning = 2,
  Error = 3,
}

export const userNotificationTypeOptions = mapEnumToOptions(UserNotificationType);
