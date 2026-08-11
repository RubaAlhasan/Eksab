import { mapEnumToOptions } from '@abp/ng.core';

export enum InvoiceStatus {
  Draft = 0,
  Sent = 1,
  Paid = 2,
  Overdue = 3,
}

export const invoiceStatusOptions = mapEnumToOptions(InvoiceStatus);
