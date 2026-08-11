import { mapEnumToOptions } from '@abp/ng.core';

export enum PointsTransactionSource {
  Purchase = 0,
  Campaign = 1,
  Referral = 2,
  Birthday = 3,
  Manual = 4,
  Reward = 5,
}

export const pointsTransactionSourceOptions = mapEnumToOptions(PointsTransactionSource);
