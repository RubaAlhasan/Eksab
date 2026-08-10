import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { AdminAuditLogFilterDto, AuditLogDetailDto, AuditLogDto } from '../audit-logs/models';

@Injectable({
  providedIn: 'root',
})
export class AuditLogsService {
  private restService = inject(RestService);
  apiName = 'Default';


  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AuditLogDetailDto>({
      method: 'GET',
      url: `/api/app/audit-log/${id}`,
    },
    { apiName: this.apiName,...config });


  getList = (input: AdminAuditLogFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<AuditLogDto>>({
      method: 'GET',
      url: '/api/app/audit-log',
      params: {
        startTime: input.startTime,
        endTime: input.endTime,
        httpMethod: input.httpMethod,
        url: input.url,
        userName: input.userName,
        hasException: input.hasException,
        httpStatusCode: input.httpStatusCode,
        sorting: input.sorting,
        skipCount: input.skipCount,
        maxResultCount: input.maxResultCount,
      },
    },
    { apiName: this.apiName,...config });
}
