import { mapEnumToOptions } from '@abp/ng.core';

export enum CampaignStatus {
  Draft = 0,
  Active = 1,
  Ended = 2,
}

export const campaignStatusOptions = mapEnumToOptions(CampaignStatus);
