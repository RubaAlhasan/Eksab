import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { RequestOtpDto } from '../otp/models';

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
}