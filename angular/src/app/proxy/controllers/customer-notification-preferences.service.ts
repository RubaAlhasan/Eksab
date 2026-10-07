import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

// Hand-added proxy — CustomerNotificationPreferencesController on the backend, not yet in the generated proxy.
export interface NotificationPreferencesDto {
  rewards: boolean;
  deals: boolean;
  offers: boolean;
}

@Injectable({
  providedIn: 'root',
})
export class CustomerNotificationPreferencesService {
  private restService = inject(RestService);
  apiName = 'Default';

  getMine = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, NotificationPreferencesDto>({
      method: 'GET',
      url: '/api/app/customer-notification-preferences/mine',
    },
    { apiName: this.apiName,...config });

  updateMine = (input: NotificationPreferencesDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, NotificationPreferencesDto>({
      method: 'PUT',
      url: '/api/app/customer-notification-preferences/mine',
      body: input,
    },
    { apiName: this.apiName,...config });
}
