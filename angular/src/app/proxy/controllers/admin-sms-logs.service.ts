import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { AdminSmsLogFilterDto, SmsLogDto } from '../sms/models';

@Injectable({
  providedIn: 'root',
})
export class AdminSmsLogsService {
  private restService = inject(RestService);
  apiName = 'Default';


  clear = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: '/api/app/admin-sms-logs',
    },
    { apiName: this.apiName,...config });


  getList = (input: AdminSmsLogFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<SmsLogDto>>({
      method: 'GET',
      url: '/api/app/admin-sms-logs',
      params: {
        filterText: input.filterText,
        sorting: input.sorting,
        skipCount: input.skipCount,
        maxResultCount: input.maxResultCount,
      },
    },
    { apiName: this.apiName,...config });
}
