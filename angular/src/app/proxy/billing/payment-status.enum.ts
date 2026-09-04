import { mapEnumToOptions } from '@abp/ng.core';

export enum PaymentStatus {
  Pending = 0,
  Succeeded = 1,
  Failed = 2,
}

export const paymentStatusOptions = mapEnumToOptions(PaymentStatus);
