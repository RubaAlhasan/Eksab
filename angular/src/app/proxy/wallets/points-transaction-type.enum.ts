import { mapEnumToOptions } from '@abp/ng.core';

export enum PointsTransactionType {
  Earn = 0,
  Redeem = 1,
  Expire = 2,
  Adjust = 3,
  Refund = 4,
}

export const pointsTransactionTypeOptions = mapEnumToOptions(PointsTransactionType);
