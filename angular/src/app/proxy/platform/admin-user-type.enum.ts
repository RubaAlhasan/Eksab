import { mapEnumToOptions } from '@abp/ng.core';

export enum AdminUserType {
  Customer = 0,
  Staff = 1,
}

export const adminUserTypeOptions = mapEnumToOptions(AdminUserType);
