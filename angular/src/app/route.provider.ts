import { RoutesService, eLayoutType } from '@abp/ng.core';
import { inject, provideAppInitializer } from '@angular/core';
export const APP_ROUTE_PROVIDER = [
  provideAppInitializer(() => {
    configureRoutes();
  }),
];
function configureRoutes() {
  const routes = inject(RoutesService);
  routes.add([
      {
        path: '/home',
        name: '::Menu:Home',
        iconClass: 'fas fa-home',
        order: 1,
        layout: eLayoutType.application,
      },
      // Admin Portal routes use eLayoutType.empty, not .application — AdminLayoutComponent
      // (angular/src/app/admin/layout/) renders its own sidebar/topbar; if these stayed .application,
      // ABP's own Lepton-X SideMenu layout would ALSO wrap them, producing double chrome. This is the
      // layout-resolution mechanism documented in NEXT_SESSION_PROMPT.md gotcha #2 — RoutesService's
      // menu tree is what DynamicLayoutComponent actually consults, not just the route's own
      // `data.layout`, so every admin leaf route needs its own explicit entry here even though
      // AdminLayoutComponent's own sidebar UI is built from a plain nav array, not from this menu tree.
      {
        path: '/admin/dashboard',
        name: '::AdminPanel:Dashboard:Title',
        iconClass: 'fas fa-gauge-high',
        order: 1.5,
        layout: eLayoutType.empty,
        requiredPolicy: 'Eksabli.Tenants.View',
      },
      {
        path: '/admin/businesses',
        name: '::Menu:AdminTenants',
        iconClass: 'fas fa-building',
        order: 2,
        layout: eLayoutType.empty,
        requiredPolicy: 'Eksabli.Tenants.View',
      },
      {
        path: '/admin/users',
        name: '::Menu:AdminUsers',
        iconClass: 'fas fa-address-book',
        order: 3,
        layout: eLayoutType.empty,
        requiredPolicy: 'Eksabli.Users.View',
      },
      {
        path: '/admin/categories',
        name: '::Menu:Categories',
        iconClass: 'fas fa-tags',
        order: 4,
        layout: eLayoutType.empty,
        requiredPolicy: 'Eksabli.Tenants.View',
      },
      {
        path: '/admin/plans',
        name: '::Menu:SubscriptionPlans',
        iconClass: 'fas fa-receipt',
        order: 5,
        layout: eLayoutType.empty,
        requiredPolicy: 'Eksabli.Tenants.View',
      },
      {
        path: '/admin/subscriptions',
        name: '::AdminPanel:Subscriptions:Title',
        iconClass: 'fas fa-credit-card',
        order: 6,
        layout: eLayoutType.empty,
        requiredPolicy: 'Eksabli.Billing.ManagePlatform',
      },
      {
        path: '/admin/support-tickets',
        name: '::Menu:SupportTickets',
        iconClass: 'fas fa-life-ring',
        order: 7,
        layout: eLayoutType.empty,
        requiredPolicy: 'Eksabli.SupportTickets.Manage',
      },
      // Layout-resolution anchors ONLY for the stock ABP pages nested under /admin in app.routes.ts
      // (Users/Roles/My Profile/Settings) — `invisible: true` so these do NOT become their own menu
      // item (real permission checks already live inside each package's own routes, not here); they
      // exist purely so findRoute()'s path-walk-up (see the /admin/businesses comment above) resolves
      // eLayoutType.empty for everything under e.g. /admin/identity/*, instead of falling through to
      // Lepton-X's stock chrome. `name` is required by the `ABP.Route` type but never shown anywhere.
      {
        path: '/admin/identity',
        name: 'Eksabli::Internal:AdminIdentityLayoutAnchor',
        invisible: true,
        layout: eLayoutType.empty,
      },
      {
        path: '/admin/setting-management',
        name: 'Eksabli::Internal:AdminSettingManagementLayoutAnchor',
        invisible: true,
        layout: eLayoutType.empty,
      },
      {
        path: '/admin/account',
        name: 'Eksabli::Internal:AdminAccountLayoutAnchor',
        invisible: true,
        layout: eLayoutType.empty,
      },
      {
        path: '/admin/tenant-management',
        name: 'Eksabli::Internal:AdminTenantManagementLayoutAnchor',
        invisible: true,
        layout: eLayoutType.empty,
      },
      // Business Portal routes — same eLayoutType.empty treatment as Admin above (BusinessLayoutComponent
      // renders its own shell; see app.routes.ts's '/business' block and business.guard.ts for the real
      // tenant-resolution-based realm gate).
      {
        path: '/business/dashboard',
        name: '::BusinessPanel:Layout:NavDashboard',
        iconClass: 'fas fa-gauge-high',
        order: 19,
        layout: eLayoutType.empty,
        requiredPolicy: 'Eksabli.Reports.Default',
      },
      {
        path: '/business/analytics',
        name: '::BusinessPanel:Layout:NavAnalytics',
        iconClass: 'fas fa-chart-line',
        order: 20,
        layout: eLayoutType.empty,
        requiredPolicy: 'Eksabli.Reports.Default',
      },
      {
        path: '/business/customers',
        name: '::BusinessPanel:Layout:NavCustomers',
        iconClass: 'fas fa-users',
        order: 21,
        layout: eLayoutType.empty,
        requiredPolicy: 'Eksabli.Memberships.View',
      },
      {
        path: '/business/employees',
        name: '::BusinessPanel:Layout:NavEmployees',
        iconClass: 'fas fa-user-tie',
        order: 22,
        layout: eLayoutType.empty,
        requiredPolicy: 'Eksabli.EmployeeAssignments.Default',
      },
      {
        path: '/business/branches',
        name: '::BusinessPanel:Layout:NavBranches',
        iconClass: 'fas fa-building',
        order: 23,
        layout: eLayoutType.empty,
        requiredPolicy: 'Eksabli.Branches.Default',
      },
      // No requiredPolicy — same shape as '/home' above; PosController has no ABP permission at all,
      // see app.routes.ts's own comment on this route for the full reasoning.
      {
        path: '/business/points',
        name: '::BusinessPanel:Layout:NavPoints',
        iconClass: 'fas fa-qrcode',
        order: 24,
        layout: eLayoutType.empty,
      },
      {
        path: '/business/rewards',
        name: '::BusinessPanel:Layout:NavRewards',
        iconClass: 'fas fa-gift',
        order: 25,
        layout: eLayoutType.empty,
        requiredPolicy: 'Eksabli.Rewards.Default',
      },
      {
        path: '/business/coupons',
        name: '::BusinessPanel:Layout:NavCoupons',
        iconClass: 'fas fa-ticket',
        order: 26,
        layout: eLayoutType.empty,
        requiredPolicy: 'Eksabli.Rewards.Default',
      },
  ]);
}
