import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { ConfigStateService } from '@abp/ng.core';
import { catchError, map, of } from 'rxjs';
import { BusinessService } from '../../proxy/controllers/business.service';
import { TenantApprovalStatus } from '../../proxy/business-profiles/tenant-approval-status.enum';

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

/**
 * Gates the entire `/business` subtree on `BusinessProfile.ApprovalStatus` — every new business
 * starts `Pending` (a manual moderation queue, see `BusinessProfile.cs`'s own comment) and can be
 * `Suspended` later by an admin, but until now nothing on the frontend even *read* that status
 * (`BusinessProfileDto` never exposed it — added alongside this guard) so a Pending/Suspended
 * business's own staff had full, unrestricted use of the portal. Redirects anywhere but Approved to
 * `/business/pending` (`BusinessPendingComponent`), which fetches the same profile itself to show
 * the right message and is deliberately a *separate top-level route*, not a child of `business` —
 * nesting it there would re-run this same guard on every redirect attempt and loop.
 *
 * "Fail open" (allow navigation) if the profile call itself errors — this is a UX gate on top of
 * real per-endpoint permission checks, not the actual security boundary, so a transient network
 * failure here shouldn't lock staff out entirely; same reasoning `MembershipAppService.JoinAsync`'s
 * own "missing BusinessProfile fails open" comment uses on the backend.
 */
export const businessApprovalGuard: CanActivateFn = () => {
  const businessService = inject(BusinessService);
  const router = inject(Router);
  return businessService.getProfile().pipe(
    map(profile =>
      profile.approvalStatus === TenantApprovalStatus.Approved ? true : router.createUrlTree(['/business/pending']),
    ),
    catchError(() => of(true)),
  );
};
