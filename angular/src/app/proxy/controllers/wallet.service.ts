import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { GetMyTransactionHistoryInput, PointsTransactionDto } from '../wallets/models';

@Injectable({
  providedIn: 'root',
})
export class WalletService {
  private restService = inject(RestService);
  apiName = 'Default';


  // `input`'s type and the `type` query param are hand-added — GetMyTransactionHistoryAsync grew an
  // optional transaction-type filter (WalletAppService.cs) that this proxy was never regenerated
  // against. Regenerate via `abp generate-proxy -t ng` when convenient.
  getMyTransactionHistory = (tenantId: string, input: GetMyTransactionHistoryInput, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<PointsTransactionDto>>({
      method: 'GET',
      url: `/api/app/wallet/${tenantId}/transactions`,
      params: { type: input.type, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
}