import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { BusinessProfileDto, UpdateBusinessProfileDto } from '../business-profiles/models';
import type { BusinessRegistrationResultDto, RegisterBusinessDto } from '../businesses/models';

@Injectable({
  providedIn: 'root',
})
export class BusinessService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  getProfile = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, BusinessProfileDto>({
      method: 'GET',
      url: '/api/app/business/profile',
    },
    { apiName: this.apiName,...config });
  

  register = (input: RegisterBusinessDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BusinessRegistrationResultDto>({
      method: 'POST',
      url: '/api/app/business/register',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  updateProfile = (input: UpdateBusinessProfileDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BusinessProfileDto>({
      method: 'PUT',
      url: '/api/app/business/profile',
      body: input,
    },
    { apiName: this.apiName,...config });
}