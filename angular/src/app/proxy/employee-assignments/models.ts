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

// Hand-added to match the real backend shape after EmployeeAssignmentAppService.InviteAsync started
// returning a temp password (see that file's own comment for why) — not yet re-generated via
// `abp generate-proxy -t ng` against a live Host; regenerate once the Host is restarted to confirm this
// matches exactly (same convention as every other hand-added field in this proxy tree).
export interface InviteEmployeeResultDto {
  assignment: EmployeeAssignmentDto;
  temporaryPassword: string;
}

export interface UpdateEmployeeAssignmentDto {
  role: EmployeeRole;
  branchId?: string | null;
}
