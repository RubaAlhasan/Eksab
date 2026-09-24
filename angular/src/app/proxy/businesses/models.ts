import type { TenantApprovalStatus } from '../business-profiles/tenant-approval-status.enum';
import type { PagedAndSortedResultRequestDto, PagedResultRequestDto } from '@abp/ng.core';

export interface AdminTenantDto {
  tenantId?: string;
  tenantName?: string;
  businessProfileId?: string;
  categoryId?: string | null;
  approvalStatus?: TenantApprovalStatus;
  creationTime?: string;
  memberCount: number;
}

export interface AdminTenantDetailStatsDto {
  ownerDisplayName?: string | null;
  ownerEmail?: string | null;
  branchCount?: number;
  pointsIssuedLast30Days?: number;
  pointsRedeemedLast30Days?: number;
  activeCampaignCount?: number;
}

export interface AdminTenantFilterDto extends PagedAndSortedResultRequestDto {
  approvalStatus?: TenantApprovalStatus | null;
  filterText?: string | null;
}

// Hand-added — CustomerBusinessController (src/Eksabli.HttpApi/Controllers/CustomerBusinessController.cs)
// already exists on the backend (customer-facing business directory: search/nearby/store-details/batch
// lookup, Approved tenants only) but its proxy was never generated. Regenerate via
// `abp generate-proxy -t ng` to replace these with the real generated versions once convenient; shapes
// below match the C# DTOs (Eksabli.Businesses namespace) field-for-field.
export interface CustomerBusinessBranchDto {
  id: string;
  name: string;
  phone?: string | null;
}

export interface CustomerBusinessDto {
  tenantId: string;
  name: string;
  categoryId?: string | null;
  categoryNameAr?: string | null;
  categoryNameEn?: string | null;
  descriptionAr?: string | null;
  descriptionEn?: string | null;
  website?: string | null;
  businessProfileId: string;
  hasLogo: boolean;
  logoBlobName?: string | null;
  branchCount: number;
  distanceKm?: number | null;
  branches: CustomerBusinessBranchDto[];
}

export interface CustomerBusinessFilterDto extends PagedResultRequestDto {
  filterText?: string | null;
  categoryId?: string | null;
  latitude?: number | null;
  longitude?: number | null;
}

export interface CustomerBusinessLookupDto {
  tenantIds: string[];
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
  displayName?: string | null;
  categoryId?: string | null;
  descriptionAr?: string | null;
  descriptionEn?: string | null;
  website?: string | null;
  instagramUrl?: string | null;
  facebookUrl?: string | null;
  branchName: string;
  branchAddress?: string | null;
  branchPhone?: string | null;
  branchLatitude?: number | null;
  branchLongitude?: number | null;
  ownerEmail: string;
  ownerPassword: string;
}
