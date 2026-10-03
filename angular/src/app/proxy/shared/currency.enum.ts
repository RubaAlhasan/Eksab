import { mapEnumToOptions } from '@abp/ng.core';

export enum Currency {
  Syp = 0,
  Usd = 1,
}

export const currencyOptions = mapEnumToOptions(Currency);
