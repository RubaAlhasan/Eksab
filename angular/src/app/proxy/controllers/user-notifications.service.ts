import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { SendUserNotificationDto, UserNotificationDto, UserNotificationListFilterDto } from '../user-notifications/models';

@Injectable({
  providedIn: 'root',
})
export class UserNotificationsService {
  private restService = inject(RestService);
  apiName = 'Default';


  getList = (input: UserNotificationListFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<UserNotificationDto>>({
      method: 'GET',
      url: '/api/app/user-notifications',
      params: { isRead: input.isRead, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });


  getUnreadCount = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, number>({
      method: 'GET',
      url: '/api/app/user-notifications/unread-count',
    },
    { apiName: this.apiName,...config });


  markAsRead = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/user-notifications/${id}/mark-as-read`,
    },
    { apiName: this.apiName,...config });


  markAllAsRead = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: '/api/app/user-notifications/mark-all-as-read',
    },
    { apiName: this.apiName,...config });


  send = (input: SendUserNotificationDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: '/api/app/user-notifications',
      body: input,
    },
    { apiName: this.apiName,...config });
}
