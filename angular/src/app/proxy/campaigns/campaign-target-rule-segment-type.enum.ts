import { mapEnumToOptions } from '@abp/ng.core';

export enum CampaignTargetRuleSegmentType {
  Tier = 0,
  Inactive = 1,
  NewCustomer = 2,
  All = 3,
}

export const campaignTargetRuleSegmentTypeOptions = mapEnumToOptions(CampaignTargetRuleSegmentType);
