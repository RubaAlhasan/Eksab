import { mapEnumToOptions } from '@abp/ng.core';

export enum EmployeeRole {
  Owner = 0,
  BranchManager = 1,
  Cashier = 2,
  MarketingManager = 3,
}

export const employeeRoleOptions = mapEnumToOptions(EmployeeRole);
