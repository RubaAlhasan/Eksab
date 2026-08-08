import type { FullAuditedEntityDto } from '@abp/ng.core';

export interface CreateUpdateOfferDto {
  branchId?: string | null;
  titleAr: string;
  titleEn: string;
  descriptionAr?: string | null;
  descriptionEn?: string | null;
  startDate: string;
  endDate: string;
  imageBlobName?: string | null;
}

export interface OfferDto extends FullAuditedEntityDto<string> {
  tenantId?: string | null;
  branchId?: string | null;
  titleAr?: string;
  titleEn?: string;
  descriptionAr?: string | null;
  descriptionEn?: string | null;
  startDate?: string;
  endDate?: string;
  imageBlobName?: string | null;
}
