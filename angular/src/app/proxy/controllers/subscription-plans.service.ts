import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { CreateUpdateSubscriptionPlanDto, SubscriptionPlanDto } from '../billing/models';

@Injectable({
  providedIn: 'root',
})
export class SubscriptionPlansService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateUpdateSubscriptionPlanDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SubscriptionPlanDto>({
      method: 'POST',
      url: '/api/app/subscription-plan',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/subscription-plan/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SubscriptionPlanDto>({
      method: 'GET',
      url: `/api/app/subscription-plan/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<SubscriptionPlanDto>>({
      method: 'GET',
      url: '/api/app/subscription-plan',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateSubscriptionPlanDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SubscriptionPlanDto>({
      method: 'PUT',
      url: `/api/app/subscription-plan/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}