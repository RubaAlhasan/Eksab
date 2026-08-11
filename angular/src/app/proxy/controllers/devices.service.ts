import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { DeviceDto, RegisterDeviceDto } from '../devices/models';

@Injectable({
  providedIn: 'root',
})
export class DevicesService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  getList = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, DeviceDto[]>({
      method: 'GET',
      url: '/api/app/devices',
    },
    { apiName: this.apiName,...config });
  

  register = (input: RegisterDeviceDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, DeviceDto>({
      method: 'POST',
      url: '/api/app/devices',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  remove = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/devices/${id}`,
    },
    { apiName: this.apiName,...config });
}