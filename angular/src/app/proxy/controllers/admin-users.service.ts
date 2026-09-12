import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { AdminCustomerDetailDto, AdminUserDto, AdminUserFilterDto } from '../platform/models';
import type { PointsTransactionDto } from '../wallets/models';

@Injectable({
  providedIn: 'root',
})
export class AdminUsersService {
  private restService = inject(RestService);
  apiName = 'Default';


  getCustomerDetail = (customerId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AdminCustomerDetailDto>({
      method: 'GET',
      url: `/api/app/admin-users/${customerId}`,
    },
    { apiName: this.apiName,...config });


  getCustomerTransactions = (membershipId: string, tenantId: string, input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<PointsTransactionDto>>({
      method: 'GET',
      url: `/api/app/admin-users/memberships/${membershipId}/transactions`,
      params: { tenantId, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });


  getList = (input: AdminUserFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<AdminUserDto>>({
      method: 'GET',
      url: '/api/app/admin-users',
      params: { filterText: input.filterText, type: input.type, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
}
