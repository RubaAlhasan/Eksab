import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { CustomerProfileDto, UpdateCustomerProfileDto } from '../customer-profiles/models';

@Injectable({
  providedIn: 'root',
})
export class CustomerProfileService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  getMyProfile = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, CustomerProfileDto>({
      method: 'GET',
      url: '/api/app/customer-profile/my',
    },
    { apiName: this.apiName,...config });
  

  updateMyProfile = (input: UpdateCustomerProfileDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CustomerProfileDto>({
      method: 'PUT',
      url: '/api/app/customer-profile/my',
      body: input,
    },
    { apiName: this.apiName,...config });
}