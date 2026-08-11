import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { CreateUpdateOfferDto, OfferDto } from '../offers/models';

@Injectable({
  providedIn: 'root',
})
export class OffersService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  create = (input: CreateUpdateOfferDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, OfferDto>({
      method: 'POST',
      url: '/api/app/offer',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/offer/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, OfferDto>({
      method: 'GET',
      url: `/api/app/offer/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<OfferDto>>({
      method: 'GET',
      url: '/api/app/offer',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateOfferDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, OfferDto>({
      method: 'PUT',
      url: `/api/app/offer/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}