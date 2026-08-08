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
        path: '/admin/businesses',
        name: '::Menu:AdminTenants',
        iconClass: 'fas fa-building',
        order: 2,
        layout: eLayoutType.empty,
        requiredPolicy: 'Eksabli.Tenants.View',
      },
      {
        path: '/admin/categories',
        name: '::Menu:Categories',
        iconClass: 'fas fa-tags',
        order: 3,
        layout: eLayoutType.empty,
        requiredPolicy: 'Eksabli.Tenants.View',
      },
      {
        path: '/admin/plans',
        name: '::Menu:SubscriptionPlans',
        iconClass: 'fas fa-receipt',
        order: 4,
        layout: eLayoutType.empty,
        requiredPolicy: 'Eksabli.Tenants.View',
      },
      {
        path: '/admin/subscriptions',
        name: '::AdminPanel:Subscriptions:Title',
        iconClass: 'fas fa-credit-card',
        order: 5,
        layout: eLayoutType.empty,
        requiredPolicy: 'Eksabli.Billing.ManagePlatform',
      },
      {
        path: '/admin/support-tickets',
        name: '::Menu:SupportTickets',
        iconClass: 'fas fa-life-ring',
        order: 6,
        layout: eLayoutType.empty,
        requiredPolicy: 'Eksabli.SupportTickets.Manage',
      },
  ]);
}
