import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { CreateUpdateRewardDto, RewardDto } from '../rewards/models';

@Injectable({
  providedIn: 'root',
})
export class RewardsService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateUpdateRewardDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, RewardDto>({
      method: 'POST',
      url: '/api/app/reward',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/reward/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, RewardDto>({
      method: 'GET',
      url: `/api/app/reward/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<RewardDto>>({
      method: 'GET',
      url: '/api/app/reward',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateRewardDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, RewardDto>({
      method: 'PUT',
      url: `/api/app/reward/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}