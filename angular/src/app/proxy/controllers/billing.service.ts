import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { ChangePlanDto, InvoiceDto, TenantSubscriptionDto, UsageDto } from '../billing/models';

@Injectable({
  providedIn: 'root',
})
export class BillingService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  changePlan = (input: ChangePlanDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TenantSubscriptionDto>({
      method: 'POST',
      url: '/api/app/billing/change-plan',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  getMyCurrentSubscription = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, TenantSubscriptionDto>({
      method: 'GET',
      url: '/api/app/billing/my-subscription',
    },
    { apiName: this.apiName,...config });
  

  getMyInvoices = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<InvoiceDto>>({
      method: 'GET',
      url: '/api/app/billing/my-invoices',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getMyUsage = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, UsageDto>({
      method: 'GET',
      url: '/api/app/billing/my-usage',
    },
    { apiName: this.apiName,...config });
}