import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { SupportTicketMetricsDto, TenantGrowthPointDto } from '../platform-reports/models';

@Injectable({
  providedIn: 'root',
})
export class AdminPlatformReportsService {
  private restService = inject(RestService);
  apiName = 'Default';


  getTenantGrowth = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, TenantGrowthPointDto[]>({
      method: 'GET',
      url: '/api/app/admin-platform-reports/tenant-growth',
    },
    { apiName: this.apiName,...config });


  getTicketMetrics = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, SupportTicketMetricsDto>({
      method: 'GET',
      url: '/api/app/admin-platform-reports/ticket-metrics',
    },
    { apiName: this.apiName,...config });
}
