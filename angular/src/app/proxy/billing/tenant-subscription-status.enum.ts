import { mapEnumToOptions } from '@abp/ng.core';

export enum TenantSubscriptionStatus {
  Trialing = 0,
  Active = 1,
  PastDue = 2,
  Cancelled = 3,
}

export const tenantSubscriptionStatusOptions = mapEnumToOptions(TenantSubscriptionStatus);
