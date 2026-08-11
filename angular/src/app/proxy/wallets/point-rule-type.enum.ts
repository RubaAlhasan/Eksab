import { mapEnumToOptions } from '@abp/ng.core';

export enum PointRuleType {
  PerCurrencyUnit = 0,
  PerVisit = 1,
}

export const pointRuleTypeOptions = mapEnumToOptions(PointRuleType);
