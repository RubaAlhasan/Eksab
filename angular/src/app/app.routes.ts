import { AuthService, ConfigStateService, PermissionService, authGuard, eLayoutType, permissionGuard } from '@abp/ng.core';
import { inject } from '@angular/core';
import { CanActivateFn, Router, Routes } from '@angular/router';
import { adminGuard, isPlatformAdmin } from './core/guards/admin.guard';
import { businessRealmGuard, isBusinessRealm } from './core/guards/business.guard';

/**
 * OAuth redirectUri always lands back on '/' — figure out where an already-authenticated visitor
 * actually belongs instead of always sending them to the customer/tenant placeholder.
 *
 * Two independent realm signals, checked in order:
 * 1. `isPlatformAdmin` — permission-based (`Eksabli.Tenants.View`, see admin.guard.ts). A platform
 *    admin always lands on `/admin`.
 * 2. `isBusinessRealm` — real tenant-resolution-based (`currentTenant.id` from ABP's own config state,
 *    see business.guard.ts's own comment for why this is a reliable signal without any custom
 *    subdomain/header tenant-selection UI). A business-staff account lands on `/business`.
 *
 * Everyone else (a Host-realm customer, or nobody authenticated) falls through to `/home`/the landing
 * page as before.
 */
const redirectAuthenticatedToHomeGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const permissionService = inject(PermissionService);
  const configState = inject(ConfigStateService);
  const router = inject(Router);
  if (!authService.isAuthenticated) return true;
  if (isPlatformAdmin(permissionService)) return router.createUrlTree(['/admin']);
  if (isBusinessRealm(configState)) return router.createUrlTree(['/business']);
  return router.createUrlTree(['/home']);
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
    // doesn't *also* wrap these routes — see that file's comment for why this matters). Host-realm
    // platform staff only — `adminGuard` gates the whole subtree; tenant-realm business staff have
    // their own separate shell/subtree at `/business` (see below), gated by `businessRealmGuard`.
    path: 'admin',
    loadComponent: () => import('./admin/layout/admin-layout.component').then(c => c.AdminLayoutComponent),
    canActivate: [authGuard, adminGuard],
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
    // Parent shell for the Business Portal — tenant-realm staff (Owner/BranchManager/Cashier/
    // MarketingManager). `BusinessLayoutComponent` renders its own sidebar/topbar (same
    // `eLayoutType.empty` treatment as Admin above — see route.provider.ts). Gated on
    // `businessRealmGuard` (core/guards/business.guard.ts) — real tenant-resolution (`currentTenant.id`
    // from ABP's own config state), not a permission heuristic; see that guard's own comment for why
    // this is reliable. Each child route still gates on its own real permission on top of that, same
    // "coarse realm guard + per-page permission" shape as Admin.
    path: 'business',
    loadComponent: () => import('./business/layout/business-layout.component').then(c => c.BusinessLayoutComponent),
    canActivate: [authGuard, businessRealmGuard],
    children: [
      {
        // Bare '/business' lands here — matches redirectAuthenticatedToHomeGuard's own destination
        // for a business-realm account and the prototype's own IA (Dashboard is the landing page).
        path: '',
        pathMatch: 'full',
        redirectTo: 'dashboard',
      },
      {
        path: 'dashboard',
        loadComponent: () =>
          import('./business/dashboard/business-dashboard.component').then(c => c.BusinessDashboardComponent),
        canActivate: [permissionGuard],
        // Whole `ReportsController` (view included) is gated on `Eksabli.Reports.Default`, no lesser
        // read to fall back to.
        data: { requiredPolicy: 'Eksabli.Reports.Default' },
      },
      {
        path: 'analytics',
        loadComponent: () =>
          import('./business/analytics/business-analytics.component').then(c => c.BusinessAnalyticsComponent),
        canActivate: [permissionGuard],
        data: { requiredPolicy: 'Eksabli.Reports.Default' },
      },
      {
        path: 'customers',
        loadComponent: () =>
          import('./business/customers/business-customers.component').then(c => c.BusinessCustomersComponent),
        canActivate: [permissionGuard],
        data: { requiredPolicy: 'Eksabli.Memberships.View' },
      },
      {
        path: 'employees',
        loadComponent: () =>
          import('./business/employees/business-employees.component').then(c => c.BusinessEmployeesComponent),
        canActivate: [permissionGuard],
        data: { requiredPolicy: 'Eksabli.EmployeeAssignments.Default' },
      },
      {
        path: 'branches',
        loadComponent: () =>
          import('./business/branches/business-branches.component').then(c => c.BusinessBranchesComponent),
        canActivate: [permissionGuard],
        data: { requiredPolicy: 'Eksabli.Branches.Default' },
      },
      {
        // No `permissionGuard`/`requiredPolicy` — deliberately. `PosController` (the Award Points tab)
        // has no ABP permission at all; it's gated by a custom staff-role check inside `PosAppService`
        // itself, not `[Authorize(EksabliPermissions...)]` (see business-points.component.ts's own
        // file comment for why). The Point Rules/Tiers tabs DO have real permissions
        // (`Eksabli.PointRules.Default`/`Eksabli.Tiers.Default`) but are shown/hidden per-tab inside
        // the component instead, since neither alone should gate the whole page.
        path: 'points',
        loadComponent: () =>
          import('./business/points/business-points.component').then(c => c.BusinessPointsComponent),
      },
      {
        path: 'rewards',
        loadComponent: () =>
          import('./business/rewards/business-rewards.component').then(c => c.BusinessRewardsComponent),
        canActivate: [permissionGuard],
        data: { requiredPolicy: 'Eksabli.Rewards.Default' },
      },
      {
        // CouponAuditController is gated on the same Eksabli.Rewards.Default permission as Rewards
        // itself — a coupon audit trail is really "Rewards" data, not a separate permission group.
        path: 'coupons',
        loadComponent: () =>
          import('./business/coupons/business-coupons.component').then(c => c.BusinessCouponsComponent),
        canActivate: [permissionGuard],
        data: { requiredPolicy: 'Eksabli.Rewards.Default' },
      },
      {
        path: 'campaigns',
        loadComponent: () =>
          import('./business/campaigns/business-campaigns.component').then(c => c.BusinessCampaignsComponent),
        canActivate: [permissionGuard],
        data: { requiredPolicy: 'Eksabli.Campaigns.Default' },
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
