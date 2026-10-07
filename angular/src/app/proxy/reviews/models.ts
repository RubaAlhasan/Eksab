import type { AuditedEntityDto, PagedResultDto } from '@abp/ng.core';

export interface ReviewDto extends AuditedEntityDto<string> {
  customerId?: string;
  rating: number;
  comment?: string | null;
  reviewerName?: string | null;
}

export interface CreateUpdateReviewDto {
  rating: number;
  comment?: string | null;
}

export interface ReviewListResultDto extends PagedResultDto<ReviewDto> {
  averageRating: number;
}
