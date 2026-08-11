import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { AdminUserDto, AdminUserFilterDto } from '../platform/models';

@Injectable({
  providedIn: 'root',
})
export class AdminUsersService {
  private restService = inject(RestService);
  apiName = 'Default';


  getList = (input: AdminUserFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<AdminUserDto>>({
      method: 'GET',
      url: '/api/app/admin-users',
      params: { filterText: input.filterText, type: input.type, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
}
