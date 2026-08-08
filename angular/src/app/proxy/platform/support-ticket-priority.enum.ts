import { mapEnumToOptions } from '@abp/ng.core';

export enum SupportTicketPriority {
  Low = 0,
  Medium = 1,
  High = 2,
  Urgent = 3,
}

export const supportTicketPriorityOptions = mapEnumToOptions(SupportTicketPriority);
