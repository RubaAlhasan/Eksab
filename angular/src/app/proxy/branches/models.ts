import type { AuditedEntityDto } from '@abp/ng.core';

export interface BranchDto extends AuditedEntityDto<string> {
  name?: string;
  address?: string | null;
  latitude?: number | null;
  longitude?: number | null;
  phone?: string | null;
  openingHoursJson?: string | null;
}

export interface CreateUpdateBranchDto {
  name: string;
  address?: string | null;
  latitude?: number | null;
  longitude?: number | null;
  phone?: string | null;
  openingHoursJson?: string | null;
}
