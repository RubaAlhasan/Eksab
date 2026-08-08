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
