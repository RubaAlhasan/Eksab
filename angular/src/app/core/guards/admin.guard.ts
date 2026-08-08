import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { PermissionService } from '@abp/ng.core';

/**
 * The permission used as the "is this account platform staff" signal throughout the Admin Portal.
 * Anyone granted this permission is Host-realm staff by construction of the backend permission model
 * (see `EksabliPermissions.Tenants.View` — gated by `[Authorize(EksabliPermissions.Tenants.View)]` on
 * `AdminTenantsController`, a Host-only, `Disable<IMultiTenant>()`-scoped controller). There is no
 * dedicated "is admin" permission and inventing one would duplicate what this already expresses.
 *
 * Not exported as a route `data.requiredPolicy` string directly because it's also needed as plain
 * TypeScript logic in `redirectAuthenticatedToHomeGuard` (see app.routes.ts) — kept as one constant so
 * the two call sites can't drift.
 */
export const ADMIN_REALM_PERMISSION = 'Eksabli.Tenants.View';

/** True if the current session holds the platform-staff signal permission. Synchronous — reads from
 *  already-loaded ABP config state, matching the same primitive `permissionGuard` itself uses
 *  internally (`PermissionService.getGrantedPolicy`), not a new/duplicate auth mechanism. */
export function isPlatformAdmin(permissionService: PermissionService): boolean {
  return permissionService.getGrantedPolicy(ADMIN_REALM_PERMISSION);
}

/**
 * Coarse gate for the entire `/admin` route subtree, on top of (not instead of) each child route's own
 * `permissionGuard` + specific `requiredPolicy`. Redirects a signed-in non-admin (business staff or
 * customer — both realms this app doesn't have a real portal for yet) to `/home` rather than showing a
 * 403, since `/home` is the one non-admin authenticated destination that actually exists today.
 */
export const adminGuard: CanActivateFn = () => {
  const permissionService = inject(PermissionService);
  const router = inject(Router);
  return isPlatformAdmin(permissionService) ? true : router.createUrlTree(['/home']);
};

/**
 * Inverse of `adminGuard` — for the handful of `/admin/*` pages that show tenant-realm business data
 * (Customers) rather than Host-realm platform data, even though they're mounted in the same shell (see
 * app.routes.ts's `/admin` comment for why the shell is shared). `permissionGuard` +
 * `data.requiredPolicy` alone does NOT exclude a platform admin from these: the seeded Host "admin"
 * role is granted **every** permission that exists at `DbMigrator` seed time (see
 * `IdentityDataSeeder`'s own behavior), including business-scoped ones like `Eksabli.Memberships.View`,
 * so a platform admin would otherwise still pass the permission check and see a page that isn't really
 * theirs (and, since `Membership`/etc. are tenant-scoped with `CurrentTenant.Id == null` for a Host
 * admin, would just render an empty, confusing list — not a data leak, but not a real page either).
 * Redirects to Businesses (a platform admin's actual landing page) rather than `/home`, since a
 * platform admin bounced off a business-only page is still platform staff, not a logged-out visitor.
 */
export const businessStaffOnlyGuard: CanActivateFn = () => {
  const permissionService = inject(PermissionService);
  const router = inject(Router);
  return isPlatformAdmin(permissionService) ? router.createUrlTree(['/admin/businesses']) : true;
};
