import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { CreateUpdateSmartOfferDto, SetSmartOfferEnabledDto, SmartOfferDto } from '../smart-offers/models';

@Injectable({
  providedIn: 'root',
})
export class SmartOffersService {
  private restService = inject(RestService);
  apiName = 'Default';

  create = (input: CreateUpdateSmartOfferDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SmartOfferDto>({
      method: 'POST',
      url: '/api/app/smart-offer',
      body: input,
    },
    { apiName: this.apiName,...config });

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/smart-offer/${id}`,
    },
    { apiName: this.apiName,...config });

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SmartOfferDto>({
      method: 'GET',
      url: `/api/app/smart-offer/${id}`,
    },
    { apiName: this.apiName,...config });

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<SmartOfferDto>>({
      method: 'GET',
      url: '/api/app/smart-offer',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });

  setEnabled = (id: string, input: SetSmartOfferEnabledDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SmartOfferDto>({
      method: 'PUT',
      url: `/api/app/smart-offer/${id}/enabled`,
      body: input,
    },
    { apiName: this.apiName,...config });

  update = (id: string, input: CreateUpdateSmartOfferDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SmartOfferDto>({
      method: 'PUT',
      url: `/api/app/smart-offer/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}
