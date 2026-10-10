import { mapEnumToOptions } from '@abp/ng.core';

export enum SmartPricingStrategy {
  Fixed = 0,
  TimeBased = 1,
}

export const smartPricingStrategyOptions = mapEnumToOptions(SmartPricingStrategy);
