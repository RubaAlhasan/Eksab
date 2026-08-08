import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { NotificationDto, NotificationListFilterDto, NotificationQuotaUsageDto, SendNotificationDto } from '../notifications/models';

@Injectable({
  providedIn: 'root',
})
export class NotificationsService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  getList = (input: NotificationListFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<NotificationDto>>({
      method: 'GET',
      url: '/api/app/notification',
      params: { campaignId: input.campaignId, status: input.status, channel: input.channel, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getQuotaUsage = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, NotificationQuotaUsageDto>({
      method: 'GET',
      url: '/api/app/notification/quota-usage',
    },
    { apiName: this.apiName,...config });
  

  send = (input: SendNotificationDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, NotificationDto>({
      method: 'POST',
      url: '/api/app/notification',
      body: input,
    },
    { apiName: this.apiName,...config });
}