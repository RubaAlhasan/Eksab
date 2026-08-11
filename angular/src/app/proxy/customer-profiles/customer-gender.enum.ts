import { mapEnumToOptions } from '@abp/ng.core';

export enum CustomerGender {
  Unspecified = 0,
  Male = 1,
  Female = 2,
}

export const customerGenderOptions = mapEnumToOptions(CustomerGender);
