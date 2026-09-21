import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { CustomerBusinessDto, CustomerBusinessFilterDto, CustomerBusinessLookupDto } from '../businesses/models';

// Hand-added proxy — see the file comment on CustomerBusinessDto in ../businesses/models.ts for why
// (CustomerBusinessController exists on the backend, was never proxied).
@Injectable({
  providedIn: 'root',
})
export class CustomerBusinessService {
  private restService = inject(RestService);
  apiName = 'Default';


  get = (tenantId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CustomerBusinessDto>({
      method: 'GET',
      url: `/api/app/customer-business/${tenantId}`,
    },
    { apiName: this.apiName,...config });


  getList = (input: CustomerBusinessFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<CustomerBusinessDto>>({
      method: 'GET',
      url: '/api/app/customer-business',
      params: { filterText: input.filterText, categoryId: input.categoryId, latitude: input.latitude, longitude: input.longitude, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });


  getMany = (input: CustomerBusinessLookupDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CustomerBusinessDto[]>({
      method: 'POST',
      url: '/api/app/customer-business/lookup',
      body: input,
    },
    { apiName: this.apiName,...config });
}
