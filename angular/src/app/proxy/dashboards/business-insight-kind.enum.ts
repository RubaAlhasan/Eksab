import { mapEnumToOptions } from '@abp/ng.core';

export enum BusinessInsightKind {
  OfferWithoutOrders = 0,
  LowStockRewards = 1,
  BuyNowExpiringSoon = 2,
  UncoveredPeakHour = 3,
  LowValueCoverage = 4,
}

export const businessInsightKindOptions = mapEnumToOptions(BusinessInsightKind);
