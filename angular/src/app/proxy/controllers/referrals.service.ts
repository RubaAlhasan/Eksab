import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { ReferralCodeDto, ReferralDto } from '../engagement/models';

@Injectable({
  providedIn: 'root',
})
export class ReferralsService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  getMyReferralCode = (tenantId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, ReferralCodeDto>({
      method: 'GET',
      url: '/api/app/referral/my-code',
      params: { tenantId },
    },
    { apiName: this.apiName,...config });
  

  getMyReferrals = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, ReferralDto[]>({
      method: 'GET',
      url: '/api/app/referral/my',
    },
    { apiName: this.apiName,...config });
}