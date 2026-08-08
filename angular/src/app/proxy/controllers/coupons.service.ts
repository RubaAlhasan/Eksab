import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { CouponDto, RedeemRewardDto, RewardDto } from '../rewards/models';

@Injectable({
  providedIn: 'root',
})
export class CouponsService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  getCatalog = (tenantId: string, input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<RewardDto>>({
      method: 'GET',
      url: `/api/app/coupon/catalog/${tenantId}`,
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getMyCoupons = (tenantId?: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CouponDto[]>({
      method: 'GET',
      url: '/api/app/coupon/my',
      params: { tenantId },
    },
    { apiName: this.apiName,...config });
  

  redeem = (input: RedeemRewardDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CouponDto>({
      method: 'POST',
      url: '/api/app/coupon/redeem',
      body: input,
    },
    { apiName: this.apiName,...config });
}