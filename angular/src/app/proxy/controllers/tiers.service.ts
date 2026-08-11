import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { CreateUpdateTierDto, TierDto } from '../wallets/models';

@Injectable({
  providedIn: 'root',
})
export class TiersService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateUpdateTierDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TierDto>({
      method: 'POST',
      url: '/api/app/tiers',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/tiers/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TierDto>({
      method: 'GET',
      url: `/api/app/tiers/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<TierDto>>({
      method: 'GET',
      url: '/api/app/tiers',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateTierDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TierDto>({
      method: 'PUT',
      url: `/api/app/tiers/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}