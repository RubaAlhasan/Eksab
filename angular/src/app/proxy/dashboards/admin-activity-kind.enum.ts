import { mapEnumToOptions } from '@abp/ng.core';

export enum AdminActivityKind {
  BusinessRegistered = 0,
  CustomerJoined = 1,
  SupportTicketOpened = 2,
}

export const adminActivityKindOptions = mapEnumToOptions(AdminActivityKind);
