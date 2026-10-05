import { mapEnumToOptions } from '@abp/ng.core';

export enum SmartOfferStatus {
  Paused = 0,
  Scheduled = 1,
  Live = 2,
  BetweenStages = 3,
  Expired = 4,
}

export const smartOfferStatusOptions = mapEnumToOptions(SmartOfferStatus);
