import type { AuditedEntityDto } from '@abp/ng.core';
import type { EmployeeRole } from './employee-role.enum';

export interface EmployeeAssignmentDto extends AuditedEntityDto<string> {
  userId?: string;
  userEmail?: string | null;
  branchId?: string | null;
  role?: EmployeeRole;
}

export interface InviteEmployeeDto {
  email: string;
  role: EmployeeRole;
  branchId?: string | null;
}

export interface UpdateEmployeeAssignmentDto {
  role: EmployeeRole;
  branchId?: string | null;
}
