import { mapEnumToOptions } from '@abp/ng.core';

export enum TenantApprovalStatus {
  Pending = 0,
  Approved = 1,
  Suspended = 2,
}

export const tenantApprovalStatusOptions = mapEnumToOptions(TenantApprovalStatus);
