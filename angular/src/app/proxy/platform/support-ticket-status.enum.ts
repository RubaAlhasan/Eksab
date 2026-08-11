import { mapEnumToOptions } from '@abp/ng.core';

export enum SupportTicketStatus {
  Open = 0,
  InProgress = 1,
  Resolved = 2,
  Closed = 3,
}

export const supportTicketStatusOptions = mapEnumToOptions(SupportTicketStatus);
