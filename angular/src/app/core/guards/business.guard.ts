import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { ConfigStateService, PermissionService } from '@abp/ng.core';
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
 * which resolves to a real tenant) to `/customer`, same fallback `adminGuard` uses for a non-admin.
 */
export const businessRealmGuard: CanActivateFn = () => {
  const configState = inject(ConfigStateService);
  const router = inject(Router);
  return isBusinessRealm(configState) ? true : router.createUrlTree(['/customer']);
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
 *
 * `{ skipHandleError: true }` is load-bearing, not decoration — `GET /api/app/business/profile` is
 * gated on `Eksabli.BusinessProfile`, which NONE of BranchManager/Cashier/MarketingManager's default
 * permissions grant (only Owner's "admin" role has it — see EmployeeRolePermissionDefaults.cs).
 * @abp/ng.core's RestService reports any non-2xx response to the global HttpErrorReporterService
 * (the full-page "[403] You are not authorized!" overlay) independently of this guard's own
 * `catchError` — without this flag, every non-Owner staff member hit that overlay on literally
 * every navigation into `/business/*` (this guard runs on the whole subtree's parent route),
 * confirmed live. Same fix as business-branches.component.ts's loadUsage().
 */
export const businessApprovalGuard: CanActivateFn = () => {
  const businessService = inject(BusinessService);
  const router = inject(Router);
  return businessService.getProfile({ skipHandleError: true }).pipe(
    map(profile =>
      profile.approvalStatus === TenantApprovalStatus.Approved ? true : router.createUrlTree(['/business/pending']),
    ),
    catchError(() => of(true)),
  );
};

/**
 * Picks where bare `/business` actually lands, replacing what used to be a hardcoded
 * `redirectTo: 'dashboard'`. Dashboard (`ReportsController`, `Eksabli.Reports.Default`) is NOT in
 * Cashier's default permission set (EmployeeRolePermissionDefaults.cs — Cashier only gets Memberships
 * .Default/.View/.Award/.Adjust and Rewards.Default/.Redeem) — every other tier (Owner/BranchManager/
 * MarketingManager) does have Reports, so this only actually changes anything for Cashier. Confirmed
 * live: a freshly invited Cashier landed straight on a 403 on their very first page after signing in,
 * simply because `redirectTo: 'dashboard'` never checked whether the current tier could see it.
 *
 * `/business/points` (Award Points / POS) is the fallback rather than e.g. `/business/customers` —
 * it's the one page with no `EksabliPermissions` gate at all (`PosController`'s own staff-role check
 * instead, see business-points.component.ts's file comment), so it's guaranteed reachable by every
 * tier, Cashier included, not just "probably fine for Cashier specifically".
 */
export const businessHomeGuard: CanActivateFn = () => {
  const permissionService = inject(PermissionService);
  const router = inject(Router);
  return router.createUrlTree([
    permissionService.getGrantedPolicy('Eksabli.Reports') ? '/business/dashboard' : '/business/points',
  ]);
};
