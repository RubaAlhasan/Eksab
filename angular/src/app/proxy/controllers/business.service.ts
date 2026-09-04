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


  // multipart/form-data, not a raw Blob body — matches [Consumes("multipart/form-data")] +
  // [FromForm] IRemoteStreamContent file on BusinessController.UploadLogoAsync; a raw Blob body sent
  // with Content-Type: application/json (RestService's default) gets rejected as 415 Unsupported Media
  // Type before it ever reaches that action. The field name "file" must match the parameter name.
  uploadLogo = (file: Blob, config?: Partial<Rest.Config>) => {
    const formData = new FormData();
    formData.append('file', file);
    return this.restService.request<any, BusinessProfileDto>({
      method: 'PUT',
      url: '/api/app/business/profile/logo',
      body: formData,
    },
    { apiName: this.apiName,...config });
  };


  removeLogo = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, BusinessProfileDto>({
      method: 'DELETE',
      url: '/api/app/business/profile/logo',
    },
    { apiName: this.apiName,...config });
}