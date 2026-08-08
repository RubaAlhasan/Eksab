import { mapEnumToOptions } from '@abp/ng.core';

export enum ReferralStatus {
  Pending = 0,
  Completed = 1,
  Rewarded = 2,
}

export const referralStatusOptions = mapEnumToOptions(ReferralStatus);
