import { mapEnumToOptions } from '@abp/ng.core';

export enum CouponStatus {
  Issued = 0,
  Redeemed = 1,
  Expired = 2,
  Cancelled = 3,
  Pending = 4,
}

export const couponStatusOptions = mapEnumToOptions(CouponStatus);
