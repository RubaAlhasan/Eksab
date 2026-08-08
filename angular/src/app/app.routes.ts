import { AuthService, PermissionService, authGuard, eLayoutType, permissionGuard } from '@abp/ng.core';
import { inject } from '@angular/core';
import { CanActivateFn, Router, Routes } from '@angular/router';
import { adminGuard, businessStaffOnlyGuard, isPlatformAdmin } from './core/guards/admin.guard';

/**
 * OAuth redirectUri always lands back on '/' — figure out where an already-authenticated visitor
 * actually belongs instead of always sending them to the customer/tenant placeholder.
 *
 * Realm detection is permission-based (`Eksabli.Tenants.View`, see admin.guard.ts), not tenantId-based.
 * A real Customers page for tenant-realm business staff now exists (`/admin/customers`, see below —
 * folded into the one Admin Portal shell, not a separate portal/route namespace; see admin-customers
 * .component.ts's file comment for why) but this guard is NOT yet extended to route tenant-realm staff
 * there — unlike the Host-realm admin case, there's no equally reliable, already-proven-in-this-codebase
 * signal for "this authenticated session belongs to business staff" (permission grants vary per
 * role/tenant; no single umbrella permission every staff member is guaranteed to hold). Business staff
 * can still reach `/admin/customers` directly by URL or a nav link once logged in — they just don't
 * land there automatically post-login yet. Solving realm detection properly is a separate, larger piece
 * of work than one page; don't guess at it here. Host-realm customers still fall back to `/home`,
 * unchanged.
 */
const redirectAuthenticatedToHomeGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const permissionService = inject(PermissionService);
  const router = inject(Router);
  if (!authService.isAuthenticated) return true;
  return router.createUrlTree([isPlatformAdmin(permissionService) ? '/admin' : '/home']);
};

export const APP_ROUTES: Routes = [
  {
    path: '',
    pathMatch: 'full',
    loadComponent: () => import('./landing/landing.component').then(c => c.LandingComponent),
    data: { layout: eLayoutType.empty },
    canActivate: [redirectAuthenticatedToHomeGuard],
  },
  {
    path: 'home',
    loadComponent: () => import('./home/home.component').then(c => c.HomeComponent),
    canActivate: [authGuard],
  },
  {
    // Parent shell for the whole Admin Portal — AdminLayoutComponent renders its own sidebar/topbar
    // (registered as `eLayoutType.empty` in route.provider.ts so ABP's own Lepton-X SideMenu layout
    // doesn't *also* wrap these routes — see that file's comment for why this matters).
    //
    // Deliberately only `authGuard` here, NOT `adminGuard` — `adminGuard` requires the Host-only
    // `Eksabli.Tenants.View` signal permission (see admin.guard.ts), which tenant-realm business staff
    // never hold by construction. The `customers` child route below needs to be reachable by that
    // realm too (it used to live under its own now-deleted `/business` shell — see admin-customers
    // .component.ts's file comment for why it was folded in here instead). Every child route already
    // enforces its OWN `permissionGuard` + specific policy (or, for the nested stock-ABP children,
    // relies on that package's own self-contained guards — see the comment further down) regardless of
    // this parent guard, so relaxing it doesn't widen access to anything: a Host admin without
    // `Eksabli.Memberships.View` still can't open Customers, and business staff without
    // `Eksabli.Tenants.View` still can't open Businesses/Categories/Plans/etc.
    path: 'admin',
    loadComponent: () => import('./admin/layout/admin-layout.component').then(c => c.AdminLayoutComponent),
    canActivate: [authGuard],
    children: [
      {
        // Bare '/admin' lands here — matches redirectAuthenticatedToHomeGuard's own destination for a
        // platform admin (see the guard's comment in this file) and the prototype's own IA (Dashboard
        // is the first thing platform staff see).
        path: '',
        pathMatch: 'full',
        redirectTo: 'dashboard',
      },
      {
        path: 'dashboard',
        loadComponent: () =>
          import('./admin/dashboard/admin-dashboard.component').then(c => c.AdminDashboardComponent),
        canActivate: [permissionGuard],
        // Same "closest real platform-staff signal" reasoning as every other admin route — no
        // dedicated Dashboard permission exists (see admin-dashboard.component.ts's file comment for
        // what's actually shown/hidden per-permission within the page itself).
        data: { requiredPolicy: 'Eksabli.Tenants.View' },
      },
      {
        path: 'businesses',
        loadComponent: () =>
          import('./admin/businesses/admin-tenants.component').then(c => c.AdminTenantsComponent),
        canActivate: [permissionGuard],
        data: { requiredPolicy: 'Eksabli.Tenants.View' },
      },
      {
        // No separate route.provider.ts entry needed — RoutesService's findRoute() walks up the path
        // by segment until it hits an exact match (see abp-ng.core's findRoute()), so this inherits
        // '/admin/businesses' own eLayoutType.empty entry automatically.
        path: 'businesses/:tenantId',
        loadComponent: () =>
          import('./admin/businesses/admin-business-details.component').then(c => c.AdminBusinessDetailsComponent),
        canActivate: [permissionGuard],
        data: { requiredPolicy: 'Eksabli.Tenants.View' },
      },
      {
        // Cross-tenant/cross-realm user directory — genuinely Host-only (see prototype/admin/
        // users.html's own subtitle), so this is the one child route gated on `Eksabli.Users.View`
        // rather than the `businessOnly`-style treatment Customers needed.
        path: 'users',
        loadComponent: () => import('./admin/users/admin-users.component').then(c => c.AdminUsersComponent),
        canActivate: [permissionGuard],
        data: { requiredPolicy: 'Eksabli.Users.View' },
      },
      {
        path: 'categories',
        loadComponent: () =>
          import('./admin/categories/admin-categories.component').then(c => c.AdminCategoriesComponent),
        canActivate: [permissionGuard],
        // Read is [AllowAnonymous] on the backend (public taxonomy) — permissionGuard here just gates
        // reaching the Admin Portal page itself, matching every other admin route's shape; Create/Edit/
        // Delete are separately permission-checked per-action inside the component (Categories.Create/
        // .Edit/.Delete), not by this route guard.
        data: { requiredPolicy: 'Eksabli.Tenants.View' },
      },
      {
        path: 'plans',
        loadComponent: () => import('./admin/plans/admin-plans.component').then(c => c.AdminPlansComponent),
        canActivate: [permissionGuard],
        // Same shape as Categories above: read is [AllowAnonymous] on the backend (public pricing
        // catalog), this guard just gates reaching the page; Create/Update/Delete are permission-checked
        // per-action inside the component against the real single permission that covers all three
        // (Eksabli.Billing.ManagePlatform — SubscriptionPlansController has no granular per-action split).
        data: { requiredPolicy: 'Eksabli.Tenants.View' },
      },
      {
        path: 'subscriptions',
        loadComponent: () =>
          import('./admin/subscriptions/admin-subscriptions.component').then(c => c.AdminSubscriptionsComponent),
        canActivate: [permissionGuard],
        // Unlike Categories/Plans, AdminSubscriptionsController has NO [AllowAnonymous] read at all —
        // Eksabli.Billing.ManagePlatform gates the whole controller, view included, with no separate
        // read-only permission to fall back to. Gating this route on Tenants.View (like every other
        // admin route) would let a viewer past the guard only to have the actual list call 403 — a
        // broken-looking load-error state instead of being blocked cleanly. Gate on the permission the
        // page actually needs instead.
        data: { requiredPolicy: 'Eksabli.Billing.ManagePlatform' },
      },
      {
        path: 'support-tickets',
        loadComponent: () =>
          import('./admin/support-tickets/admin-support-tickets.component').then(c => c.AdminSupportTicketsComponent),
        canActivate: [permissionGuard],
        // Whole controller queue (GetListAsync) is gated on SupportTickets.Manage, no lesser read to
        // fall back to — same shape as Subscriptions above, not Tenants.View.
        data: { requiredPolicy: 'Eksabli.SupportTickets.Manage' },
      },
      {
        // Tenant-realm data (Memberships/Followers), reachable by business staff — gated on its own
        // real policy, not Tenants.View (business staff don't hold that Host-only permission). See the
        // parent '/admin' route's comment above for why the coarse `adminGuard` was relaxed to allow
        // this, and admin-customers.component.ts's file comment for why this isn't a separate portal.
        // `businessStaffOnlyGuard` additionally EXCLUDES platform admins — `permissionGuard` alone
        // isn't enough here since the seeded Host admin role holds every permission including
        // `Eksabli.Memberships.View` (see that guard's own comment in admin.guard.ts).
        path: 'customers',
        loadComponent: () =>
          import('./admin/customers/admin-customers.component').then(c => c.AdminCustomersComponent),
        canActivate: [permissionGuard, businessStaffOnlyGuard],
        data: { requiredPolicy: 'Eksabli.Memberships.View' },
      },
      // Stock ABP UI (Users/Roles/My Profile/Settings), nested here — not just linked at their
      // existing top-level '/identity', '/account', '/setting-management' paths below — so they
      // render inside AdminLayoutComponent's own shell instead of Lepton-X's stock SideMenu chrome.
      // No extra canActivate/requiredPolicy needed at this mount point: `createRoutes()` from each
      // package already ships its own `authGuard`/`permissionGuard` + per-leaf `data.requiredPolicy`
      // (confirmed by reading each package's compiled output — `AbpIdentity.Roles`/`.Users` for
      // Identity, `authGuard` for Account) — these entries just relocate where the SAME real guards
      // render, they don't change what's guarded. The original top-level mounts below are left in
      // place (not removed): '/account' specifically must stay reachable pre-auth for the actual
      // login flow, and leaving '/identity' + '/setting-management' reachable at their original
      // paths too is a harmless, zero-cost fallback (e.g. any stray link generated by the packages
      // themselves) — just no longer what our own nav links to.
      {
        path: 'identity',
        loadChildren: () => import('@abp/ng.identity').then(c => c.createRoutes()),
      },
      {
        path: 'setting-management',
        loadChildren: () => import('@abp/ng.setting-management').then(c => c.createRoutes()),
      },
      {
        path: 'account',
        loadChildren: () => import('@abp/ng.account').then(c => c.createRoutes()),
      },
      {
        // Raw ABP Tenant Management (`Volo.Abp.TenantManagement.Tenant` — connection strings,
        // deactivate/delete) — a DIFFERENT concept from our own "Businesses" page (`BusinessProfile`
        // approval workflow), even though both reuse the word "tenant"; see admin-tenants.component.ts.
        // Same self-contained-guards reasoning as identity/setting-management/account above.
        path: 'tenant-management',
        loadChildren: () => import('@abp/ng.tenant-management').then(c => c.createRoutes()),
      },
    ],
  },
  {
    path: 'account',
    loadChildren: () => import('@abp/ng.account').then(c => c.createRoutes()),
  },
  {
    path: 'identity',
    loadChildren: () => import('@abp/ng.identity').then(c => c.createRoutes()),
  },
  {
    path: 'tenant-management',
    loadChildren: () => import('@abp/ng.tenant-management').then(c => c.createRoutes()),
  },
  {
    path: 'setting-management',
    loadChildren: () => import('@abp/ng.setting-management').then(c => c.createRoutes()),
  },
];
