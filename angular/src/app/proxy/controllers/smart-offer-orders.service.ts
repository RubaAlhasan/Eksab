import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { RejectSmartOfferOrderDto, SmartOfferOrderCodeDto, SmartOfferOrderStaffDto } from '../smart-offers/models';

@Injectable({
  providedIn: 'root',
})
export class SmartOfferOrdersService {
  private restService = inject(RestService);
  apiName = 'Default';

  complete = (input: SmartOfferOrderCodeDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SmartOfferOrderStaffDto>({
      method: 'POST',
      url: '/api/app/smart-offer-order/complete',
      body: input,
    },
    { apiName: this.apiName,...config });

  getHistory = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<SmartOfferOrderStaffDto>>({
      method: 'GET',
      url: '/api/app/smart-offer-order/history',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });

  lookup = (input: SmartOfferOrderCodeDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SmartOfferOrderStaffDto>({
      method: 'POST',
      url: '/api/app/smart-offer-order/lookup',
      body: input,
    },
    { apiName: this.apiName,...config });

  reject = (input: RejectSmartOfferOrderDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SmartOfferOrderStaffDto>({
      method: 'POST',
      url: '/api/app/smart-offer-order/reject',
      body: input,
    },
    { apiName: this.apiName,...config });
}
