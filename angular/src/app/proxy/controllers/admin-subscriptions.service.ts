import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { AdminInvoiceFilterDto, AdminSubscriptionFilterDto, AdminSubscriptionStatsDto, InvoiceDto, MrrTrendPointDto, RecordManualPaymentDto, TenantSubscriptionDto } from '../billing/models';

@Injectable({
  providedIn: 'root',
})
export class AdminSubscriptionsService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  getInvoices = (input: AdminInvoiceFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<InvoiceDto>>({
      method: 'GET',
      url: '/api/app/admin-subscriptions/invoices',
      params: { status: input.status, tenantSubscriptionId: input.tenantSubscriptionId, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getList = (input: AdminSubscriptionFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<TenantSubscriptionDto>>({
      method: 'GET',
      url: '/api/app/admin-subscriptions',
      params: { status: input.status, tenantId: input.tenantId, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  recordManualPayment = (input: RecordManualPaymentDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, InvoiceDto>({
      method: 'POST',
      url: '/api/app/admin-subscriptions/record-manual-payment',
      body: input,
    },
    { apiName: this.apiName,...config });


  getStats = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, AdminSubscriptionStatsDto>({
      method: 'GET',
      url: '/api/app/admin-subscriptions/stats',
    },
    { apiName: this.apiName,...config });


  getMrrTrend = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, MrrTrendPointDto[]>({
      method: 'GET',
      url: '/api/app/admin-subscriptions/mrr-trend',
    },
    { apiName: this.apiName,...config });
}