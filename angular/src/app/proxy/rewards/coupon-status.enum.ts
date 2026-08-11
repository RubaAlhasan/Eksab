import { mapEnumToOptions } from '@abp/ng.core';

export enum CouponStatus {
  Issued = 0,
  Redeemed = 1,
  Expired = 2,
  Cancelled = 3,
}

export const couponStatusOptions = mapEnumToOptions(CouponStatus);
