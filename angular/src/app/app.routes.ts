import { AuthService, PermissionService, authGuard, eLayoutType, permissionGuard } from '@abp/ng.core';
import { inject } from '@angular/core';
import { CanActivateFn, Router, Routes } from '@angular/router';
import { adminGuard, isPlatformAdmin } from './core/guards/admin.guard';

/**
 * OAuth redirectUri always lands back on '/' — figure out where an already-authenticated visitor
 * actually belongs instead of always sending them to the customer/tenant placeholder.
 *
 * Realm detection is permission-based (`Eksabli.Tenants.View`, see admin.guard.ts), not tenantId-based:
 * this app currently has no real Business Portal or customer web area to route a tenant-realm staff
 * member or a Host-realm customer to, so both fall back to the same `/home` placeholder they already
 * used before this fix — only the Admin branch is new. Do not extend this to a `/business` redirect
 * until that portal actually exists; a guard pointing at a route that isn't there is worse than the
 * status quo.
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
    path: 'admin',
    loadComponent: () => import('./admin/layout/admin-layout.component').then(c => c.AdminLayoutComponent),
    canActivate: [authGuard, adminGuard],
    children: [
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
