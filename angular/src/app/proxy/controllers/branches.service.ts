import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { BranchDto, CreateUpdateBranchDto } from '../branches/models';

@Injectable({
  providedIn: 'root',
})
export class BranchesService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateUpdateBranchDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BranchDto>({
      method: 'POST',
      url: '/api/app/branches',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/branches/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BranchDto>({
      method: 'GET',
      url: `/api/app/branches/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<BranchDto>>({
      method: 'GET',
      url: '/api/app/branches',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateBranchDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BranchDto>({
      method: 'PUT',
      url: `/api/app/branches/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}