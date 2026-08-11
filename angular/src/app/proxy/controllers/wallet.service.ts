import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { PointsTransactionDto } from '../wallets/models';

@Injectable({
  providedIn: 'root',
})
export class WalletService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  getMyTransactionHistory = (tenantId: string, input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<PointsTransactionDto>>({
      method: 'GET',
      url: `/api/app/wallet/${tenantId}/transactions`,
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
}