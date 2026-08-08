import { mapEnumToOptions } from '@abp/ng.core';

export enum MembershipStatus {
  Active = 0,
  Frozen = 1,
}

export const membershipStatusOptions = mapEnumToOptions(MembershipStatus);
