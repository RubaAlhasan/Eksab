import { mapEnumToOptions } from '@abp/ng.core';

export enum RewardType {
  Discount = 0,
  FreeProduct = 1,
  GiftCard = 2,
}

export const rewardTypeOptions = mapEnumToOptions(RewardType);
