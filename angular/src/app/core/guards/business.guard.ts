import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { ConfigStateService } from '@abp/ng.core';

/**
 * True if the current session resolves to a real tenant — i.e. this is a tenant-realm business-staff
 * account (Owner/BranchManager/Cashier/MarketingManager), not a Host-realm account (platform admin OR
 * a Host-realm customer, both of which always have a null tenant).
 *
 * Reads `currentTenant.id` from ABP's own `ConfigStateService` (populated by the `/api/abp/application-
 * configuration` endpoint every authenticated request already calls on app init) rather than inventing
 * a new resolution mechanism. This is reliable specifically because of how tenant resolution actually
 * works in this app (confirmed by reading `EksabliHttpApiHostModule.cs`'s `app.UseMultiTenancy()`
 * wiring — no custom subdomain/path/header resolver is configured, only ABP's own default chain): for
 * an authenticated request, ABP's built-in `CurrentUserTenantResolveContributor` resolves
 * `CurrentTenant.Id` straight from the logged-in user's own account (business-staff `IdentityUser`
 * records are created *inside* their tenant's identity space at registration — see
 * `BusinessAppService.RegisterAsync`/`EmployeeAssignmentAppService.InviteAsync`). So the
 * application-configuration response's `currentTenant.id` genuinely reflects "which tenant does this
 * logged-in account belong to", not a URL/header/query-string selection — there is no separate
 * tenant-picker UI anywhere in this app, and none is needed for this signal to be correct.
 */
export function isBusinessRealm(configState: ConfigStateService): boolean {
  const currentTenant = configState.getOne('currentTenant') as { id?: string | null } | undefined;
  return !!currentTenant?.id;
}

/**
 * Coarse gate for the entire `/business` route subtree — mirrors `adminGuard`'s shape (admin.guard.ts).
 * Redirects a signed-in non-business-realm account (platform admin or Host-realm customer, neither of
 * which resolves to a real tenant) to `/home`, same fallback `adminGuard` uses for a non-admin.
 */
export const businessRealmGuard: CanActivateFn = () => {
  const configState = inject(ConfigStateService);
  const router = inject(Router);
  return isBusinessRealm(configState) ? true : router.createUrlTree(['/home']);
};
