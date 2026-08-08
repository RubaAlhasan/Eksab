import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { AwardPointsByCustomerIdDto, AwardPointsByQrDto, AwardPointsResultDto, ConfirmRedemptionDto, CustomerLookupResultDto, ManualAdjustDto, PhoneLookupDto, RedemptionConfirmationDto } from '../pos/models';

@Injectable({
  providedIn: 'root',
})
export class PosService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  awardPointsByCustomerId = (customerId: string, input: AwardPointsByCustomerIdDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AwardPointsResultDto>({
      method: 'POST',
      url: `/api/app/pos/award-points/customer/${customerId}`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  awardPointsByQr = (input: AwardPointsByQrDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AwardPointsResultDto>({
      method: 'POST',
      url: '/api/app/pos/award-points/qr',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  confirmRedemption = (input: ConfirmRedemptionDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, RedemptionConfirmationDto>({
      method: 'POST',
      url: '/api/app/pos/confirm-redemption',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  lookupCustomerByPhone = (input: PhoneLookupDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CustomerLookupResultDto>({
      method: 'POST',
      url: '/api/app/pos/lookup-by-phone',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  manualAdjust = (input: ManualAdjustDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AwardPointsResultDto>({
      method: 'POST',
      url: '/api/app/pos/adjust',
      body: input,
    },
    { apiName: this.apiName,...config });
}