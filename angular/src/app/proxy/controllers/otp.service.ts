import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { RegisterCustomerDto, RequestOtpDto } from '../otp/models';

@Injectable({
  providedIn: 'root',
})
export class OtpService {
  private restService = inject(RestService);
  apiName = 'Default';


  requestOtp = (input: RequestOtpDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: '/api/app/otp/request',
      body: input,
    },
    { apiName: this.apiName,...config });


  // Hand-added — see the file comment on RegisterCustomerDto in ../otp/models.ts for why
  // (OtpController.RegisterAsync exists on the backend, was never proxied).
  register = (input: RegisterCustomerDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: '/api/app/otp/register',
      body: input,
    },
    { apiName: this.apiName,...config });
}