import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { CustomerCampaignDto } from '../campaigns/models';

// Hand-added proxy — see the file comment on CustomerCampaignDto in ../campaigns/models.ts for why
// (CustomerCampaignController exists on the backend, was never proxied).
@Injectable({
  providedIn: 'root',
})
export class CustomerCampaignService {
  private restService = inject(RestService);
  apiName = 'Default';


  getForBusiness = (tenantId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CustomerCampaignDto[]>({
      method: 'GET',
      url: `/api/app/customer-campaign/business/${tenantId}`,
    },
    { apiName: this.apiName,...config });


  getMyFeed = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, CustomerCampaignDto[]>({
      method: 'GET',
      url: '/api/app/customer-campaign/my',
    },
    { apiName: this.apiName,...config });
}
