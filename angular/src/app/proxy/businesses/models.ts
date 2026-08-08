import type { TenantApprovalStatus } from '../business-profiles/tenant-approval-status.enum';
import type { PagedAndSortedResultRequestDto } from '@abp/ng.core';

export interface AdminTenantDto {
  tenantId?: string;
  tenantName?: string;
  businessProfileId?: string;
  categoryId?: string | null;
  approvalStatus?: TenantApprovalStatus;
  creationTime?: string;
}

export interface AdminTenantFilterDto extends PagedAndSortedResultRequestDto {
  approvalStatus?: TenantApprovalStatus | null;
  filterText?: string | null;
}

export interface BusinessRegistrationResultDto {
  tenantId?: string;
  tenantName?: string;
  businessProfileId?: string;
  branchId?: string;
  ownerUserId?: string;
}

export interface RegisterBusinessDto {
  businessName: string;
  categoryId?: string | null;
  descriptionAr?: string | null;
  descriptionEn?: string | null;
  website?: string | null;
  branchName: string;
  branchAddress?: string | null;
  branchPhone?: string | null;
  ownerEmail: string;
  ownerPassword: string;
}
