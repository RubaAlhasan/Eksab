import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { JoinBusinessDto, MemberDto, MemberFilterDto, MembershipDto, WalletQrTokenResultDto } from '../memberships/models';
import type { PointsWalletDto } from '../wallets/models';
import type { SmartDealSaleDto, SmartDealSaleFilterDto } from '../reports/models';

@Injectable({
  providedIn: 'root',
})
export class MembershipsService {
  private restService = inject(RestService);
  apiName = 'Default';


  freeze = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/memberships/${id}/freeze`,
    },
    { apiName: this.apiName,...config });


  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MemberDto>({
      method: 'GET',
      url: `/api/app/memberships/${id}`,
    },
    { apiName: this.apiName,...config });

  getSmartDealSales = (id: string, input: SmartDealSaleFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<SmartDealSaleDto>>({
      method: 'GET',
      url: `/api/app/memberships/${id}/smart-deal-sales`,
      params: { search: input.search, from: input.from, to: input.to, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });


  getMembers = (input: MemberFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<MemberDto>>({
      method: 'GET',
      url: '/api/app/memberships',
      params: { filterText: input.filterText, tierId: input.tierId, status: input.status, hasEarnedPointsAtLeastOnce: input.hasEarnedPointsAtLeastOnce, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });


  getMyMemberships = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, MembershipDto[]>({
      method: 'GET',
      url: '/api/app/memberships/my',
    },
    { apiName: this.apiName,...config });
  

  getMyWalletQrToken = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, WalletQrTokenResultDto>({
      method: 'POST',
      url: '/api/app/memberships/my/wallet-qr-token',
    },
    { apiName: this.apiName,...config });
  

  getMyWallets = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, PointsWalletDto[]>({
      method: 'GET',
      url: '/api/app/memberships/my/wallets',
    },
    { apiName: this.apiName,...config });
  

  join = (input: JoinBusinessDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MembershipDto>({
      method: 'POST',
      url: '/api/app/memberships/join',
      body: input,
    },
    { apiName: this.apiName,...config });


  leave = (tenantId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/memberships/${tenantId}/leave`,
    },
    { apiName: this.apiName,...config });


  reactivate = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/memberships/${id}/reactivate`,
    },
    { apiName: this.apiName,...config });
}