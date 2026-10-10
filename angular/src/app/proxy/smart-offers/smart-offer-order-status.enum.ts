import { mapEnumToOptions } from '@abp/ng.core';

export enum SmartOfferOrderStatus {
  Pending = 0,
  Completed = 1,
  Cancelled = 2,
  Rejected = 3,
  Expired = 4,
}

export const smartOfferOrderStatusOptions = mapEnumToOptions(SmartOfferOrderStatus);
