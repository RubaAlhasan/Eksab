import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';

// Hand-added, matching the real backend (AccountStatusController) — not yet re-generated via
// `abp generate-proxy -t ng` against a live Host; regenerate once the Host is restarted to confirm this
// matches exactly (same convention as every other hand-added proxy in this tree).
@Injectable({
  providedIn: 'root',
})
export class AccountStatusService {
  private restService = inject(RestService);
  apiName = 'Default';

  getMustChangePassword = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, boolean>({
      method: 'GET',
      url: '/api/app/account-status/must-change-password',
    },
    { apiName: this.apiName, ...config });
}
