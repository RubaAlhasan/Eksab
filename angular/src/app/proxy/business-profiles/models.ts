import type { AuditedEntityDto } from '@abp/ng.core';
import type { TenantApprovalStatus } from './tenant-approval-status.enum';

export interface BusinessProfileDto extends AuditedEntityDto<string> {
  tenantId?: string | null;
  categoryId?: string | null;
  logoBlobName?: string | null;
  descriptionAr?: string | null;
  descriptionEn?: string | null;
  website?: string | null;
  socialLinksJson?: string | null;
  approvalStatus: TenantApprovalStatus;
}

export interface UpdateBusinessProfileDto {
  categoryId?: string | null;
  descriptionAr?: string | null;
  descriptionEn?: string | null;
  website?: string | null;
  socialLinksJson?: string | null;
}
