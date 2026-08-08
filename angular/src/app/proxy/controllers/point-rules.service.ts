import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { CreateUpdatePointRuleDto, PointRuleDto } from '../wallets/models';

@Injectable({
  providedIn: 'root',
})
export class PointRulesService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateUpdatePointRuleDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PointRuleDto>({
      method: 'POST',
      url: '/api/app/point-rules',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/point-rules/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PointRuleDto>({
      method: 'GET',
      url: `/api/app/point-rules/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<PointRuleDto>>({
      method: 'GET',
      url: '/api/app/point-rules',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdatePointRuleDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PointRuleDto>({
      method: 'PUT',
      url: `/api/app/point-rules/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}