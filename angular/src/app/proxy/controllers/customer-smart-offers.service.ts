import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { PagedResultDto, PagedAndSortedResultRequestDto } from '@abp/ng.core';
import type {
  CustomerSmartOfferDetailsDto,
  CustomerSmartOfferListDto,
  CustomerSmartOfferOrderDto,
  GetMySmartOfferOrdersInput,
  PlaceSmartOfferOrderDto,
  SmartOfferOrderDto,
  SmartOfferWatchDto,
} from '../smart-offers/models';

@Injectable({
  providedIn: 'root',
})
export class CustomerSmartOffersService {
  private restService = inject(RestService);
  apiName = 'Default';

  getOffers = (tenantId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CustomerSmartOfferListDto>({
      method: 'GET',
      url: `/api/app/customer-smart-offer/tenant/${tenantId}`,
    },
    { apiName: this.apiName,...config });

  getFeed = (maxResultCount = 30, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CustomerSmartOfferListDto>({
      method: 'GET',
      url: '/api/app/customer-smart-offer/feed',
      params: { maxResultCount },
    },
    { apiName: this.apiName,...config });

  getMyOrders = (input: GetMySmartOfferOrdersInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<CustomerSmartOfferOrderDto>>({
      method: 'GET',
      url: '/api/app/customer-smart-offer/orders/mine',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount, filter: input.filter },
    },
    { apiName: this.apiName,...config });

  getOfferDetails = (tenantId: string, offerId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CustomerSmartOfferDetailsDto>({
      method: 'GET',
      url: `/api/app/customer-smart-offer/tenant/${tenantId}/offers/${offerId}`,
    },
    { apiName: this.apiName,...config });

  placeOrder = (input: PlaceSmartOfferOrderDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SmartOfferOrderDto>({
      method: 'POST',
      url: '/api/app/customer-smart-offer/orders',
      body: input,
    },
    { apiName: this.apiName,...config });

  getMyOrder = (tenantId: string, orderId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SmartOfferOrderDto>({
      method: 'GET',
      url: `/api/app/customer-smart-offer/orders/${tenantId}/${orderId}`,
    },
    { apiName: this.apiName,...config });

  cancelMyOrder = (tenantId: string, orderId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SmartOfferOrderDto>({
      method: 'POST',
      url: `/api/app/customer-smart-offer/orders/${tenantId}/${orderId}/cancel`,
    },
    { apiName: this.apiName,...config });

  // "Tell me when this price drops" — PUT/DELETE, so repeating either call is harmless.
  watchPrice = (tenantId: string, offerId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'PUT',
      url: `/api/app/customer-smart-offer/tenant/${tenantId}/offers/${offerId}/price-watch`,
    },
    { apiName: this.apiName,...config });

  unwatchPrice = (tenantId: string, offerId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/customer-smart-offer/tenant/${tenantId}/offers/${offerId}/price-watch`,
    },
    { apiName: this.apiName,...config });

  getMyPriceWatches = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, SmartOfferWatchDto[]>({
      method: 'GET',
      url: '/api/app/customer-smart-offer/price-watches/mine',
    },
    { apiName: this.apiName,...config });
}
