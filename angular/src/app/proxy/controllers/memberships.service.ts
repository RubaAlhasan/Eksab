import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { JoinBusinessDto, MembershipDto, WalletQrTokenResultDto } from '../memberships/models';
import type { PointsWalletDto } from '../wallets/models';

@Injectable({
  providedIn: 'root',
})
export class MembershipsService {
  private restService = inject(RestService);
  apiName = 'Default';
  

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
}