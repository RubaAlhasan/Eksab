import { mapEnumToOptions } from '@abp/ng.core';

export enum CampaignType {
  Birthday = 0,
  DoublePoints = 1,
  SpendXGetY = 2,
  WinBack = 3,
  Vip = 4,
  NewCustomer = 5,
  Referral = 6,
}

export const campaignTypeOptions = mapEnumToOptions(CampaignType);
