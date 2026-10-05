import { mapEnumToOptions } from '@abp/ng.core';

export enum CustomerDealOrderFilter {
  All = 0,
  Active = 1,
  Completed = 2,
  Closed = 3,
}

export const customerDealOrderFilterOptions = mapEnumToOptions(CustomerDealOrderFilter);
