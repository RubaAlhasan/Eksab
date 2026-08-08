import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { AdminTenantDto, AdminTenantFilterDto } from '../businesses/models';

@Injectable({
  providedIn: 'root',
})
export class AdminTenantsService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  approve = (tenantId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AdminTenantDto>({
      method: 'POST',
      url: `/api/app/admin-tenants/${tenantId}/approve`,
    },
    { apiName: this.apiName,...config });
  

  get = (tenantId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AdminTenantDto>({
      method: 'GET',
      url: `/api/app/admin-tenants/${tenantId}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: AdminTenantFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<AdminTenantDto>>({
      method: 'GET',
      url: '/api/app/admin-tenants',
      params: { approvalStatus: input.approvalStatus, filterText: input.filterText, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  suspend = (tenantId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AdminTenantDto>({
      method: 'POST',
      url: `/api/app/admin-tenants/${tenantId}/suspend`,
    },
    { apiName: this.apiName,...config });
}