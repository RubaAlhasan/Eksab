import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { CreateUpdateReviewDto, ReviewDto, ReviewListResultDto } from '../reviews/models';

@Injectable({
  providedIn: 'root',
})
export class ReviewsService {
  private restService = inject(RestService);
  apiName = 'Default';


  getList = (tenantId: string, input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ReviewListResultDto>({
      method: 'GET',
      url: `/api/app/review/${tenantId}`,
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });


  getMyReview = (tenantId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ReviewDto | null>({
      method: 'GET',
      url: `/api/app/review/${tenantId}/my`,
    },
    { apiName: this.apiName,...config });


  createOrUpdateMyReview = (tenantId: string, input: CreateUpdateReviewDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ReviewDto>({
      method: 'PUT',
      url: `/api/app/review/${tenantId}/my`,
      body: input,
    },
    { apiName: this.apiName,...config });


  deleteMyReview = (tenantId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/review/${tenantId}/my`,
    },
    { apiName: this.apiName,...config });


  moderateDelete = (reviewId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/review/moderate/${reviewId}`,
    },
    { apiName: this.apiName,...config });
}
