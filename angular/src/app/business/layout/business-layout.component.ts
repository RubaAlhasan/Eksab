import { Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import {
  AuthService,
  ConfigStateService,
  LocalizationPipe,
  PermissionService,
  RouteBasedCultureUrlService,
  SessionStateService,
  getLocaleDirection,
} from '@abp/ng.core';
import { NotificationBellComponent } from '../../shared/components/notification-bell/notification-bell.component';

interface BusinessNavItem {
  labelKey: string;
  icon: string;
  link: string;
  permission: string;
}

/**
 * Business Portal shell — tenant-realm business staff (Owner/BranchManager/Cashier/MarketingManager),
 * mounted at `/business` (see app.routes.ts), gated on `businessRealmGuard` (core/guards/
 * business.guard.ts) rather than a permission check — realm is what determines whether this shell is
 * reachable at all, individual pages still gate on their own real permission on top of that.
 *
 * History: this shell existed earlier this session, was deleted when Customers was folded into the
 * Admin Portal shell at the user's explicit request ("must be under admin"), then recreated here at
 * the user's later, equally explicit reversal ("employees ... must be under business/employees" +
 * "when login business show page related to business like prototype") — both real, both honored when
 * asked; don't take either as more "correct" than the other, this is genuinely what was requested each
 * time. `businessRealmGuard` (real tenant-resolution-based realm detection, not a permission heuristic)
 * is the one piece that's actually new/better this time — see that file's own comment.
 *
 * Deliberately near-identical to `AdminLayoutComponent` (admin/layout/) by design — same shell shape —
 * and deliberately NOT OnPush, same reasoning as that component (a shell wrapping arbitrary routed
 * content shouldn't assume every descendant uses signals).
 */
@Component({
  selector: 'app-business-layout',
  templateUrl: './business-layout.component.html',
  styleUrls: ['./business-layout.component.scss'],
  imports: [RouterOutlet, RouterLink, RouterLinkActive, LocalizationPipe, NotificationBellComponent],
})
export class BusinessLayoutComponent {
  private readonly authService = inject(AuthService);
  private readonly permissionService = inject(PermissionService);
  private readonly configState = inject(ConfigStateService);
  private readonly sessionState = inject(SessionStateService);
  private readonly cultureUrlService = inject(RouteBasedCultureUrlService);

  protected readonly mobileNavOpen = signal(false);
  protected readonly darkMode = signal(false);
  protected readonly langMenuOpen = signal(false);

  /** Only real, already-built pages — no placeholder/disabled entries (same rule as Admin Portal's
   *  own `ADMIN_NAV`). Add each new page's entry here the same turn it's build-verified. */
  private static readonly NAV: BusinessNavItem[] = [
    { labelKey: '::BusinessPanel:Layout:NavDashboard', icon: 'fa-gauge-high', link: '/business/dashboard', permission: 'Eksabli.Reports' },
    { labelKey: '::BusinessPanel:Layout:NavAnalytics', icon: 'fa-chart-line', link: '/business/analytics', permission: 'Eksabli.Reports' },
    { labelKey: '::BusinessPanel:Layout:NavCustomers', icon: 'fa-users', link: '/business/customers', permission: 'Eksabli.Memberships.View' },
    { labelKey: '::BusinessPanel:Layout:NavEmployees', icon: 'fa-user-tie', link: '/business/employees', permission: 'Eksabli.EmployeeAssignments' },
    { labelKey: '::BusinessPanel:Layout:NavBranches', icon: 'fa-building', link: '/business/branches', permission: 'Eksabli.Branches' },
    // Empty permission = always granted (PermissionService.getGrantedPolicy's own "no key" shape,
    // same convention as AdminLayoutComponent's `AbpAccount::MyAccount` entry) — PosController has no
    // ABP permission at all; see business-points.component.ts's file comment for why.
    { labelKey: '::BusinessPanel:Layout:NavPoints', icon: 'fa-qrcode', link: '/business/points', permission: '' },
    { labelKey: '::BusinessPanel:Layout:NavRewards', icon: 'fa-gift', link: '/business/rewards', permission: 'Eksabli.Rewards' },
    { labelKey: '::BusinessPanel:Layout:NavCoupons', icon: 'fa-ticket', link: '/business/coupons', permission: 'Eksabli.Rewards' },
    { labelKey: '::BusinessPanel:Layout:NavCampaigns', icon: 'fa-bullhorn', link: '/business/campaigns', permission: 'Eksabli.Campaigns' },
    { labelKey: '::BusinessPanel:Layout:NavNotifications', icon: 'fa-bell', link: '/business/notifications', permission: 'Eksabli.Notifications.Send' },
    { labelKey: '::BusinessPanel:Layout:NavSubscription', icon: 'fa-credit-card', link: '/business/subscription', permission: 'Eksabli.Billing.ManageOwn' },
    { labelKey: '::BusinessPanel:Layout:NavBilling', icon: 'fa-receipt', link: '/business/billing', permission: 'Eksabli.Billing.ManageOwn' },
    { labelKey: '::BusinessPanel:Layout:NavTransactions', icon: 'fa-file-invoice-dollar', link: '/business/transactions', permission: 'Eksabli.Reports.Export' },
    { labelKey: '::BusinessPanel:Layout:NavSettings', icon: 'fa-gear', link: '/business/settings', permission: 'Eksabli.BusinessProfile' },
    // Empty permission — same shape as Points above; no ABP permission gates this page, see
    // app.routes.ts's own comment on this route for why.
    { labelKey: '::BusinessPanel:Layout:NavSupportTickets', icon: 'fa-life-ring', link: '/business/support-tickets', permission: '' },
  ];

  protected readonly navItems = computed<BusinessNavItem[]>(() =>
    BusinessLayoutComponent.NAV.filter((item) => this.permissionService.getGrantedPolicy(item.permission)),
  );

  protected readonly currentUserName = computed(() => {
    const currentUser = this.configState.getOne('currentUser') as { userName?: string } | undefined;
    return currentUser?.userName ?? 'Staff';
  });

  protected readonly languages = computed(() => {
    const localization = this.configState.getOne('localization') as { languages?: { cultureName: string; displayName: string }[] } | undefined;
    return localization?.languages ?? [];
  });

  protected readonly currentLanguage = toSignal(this.sessionState.getLanguage$(), {
    initialValue: this.sessionState.getLanguage(),
  });

  protected readonly direction = computed(() => getLocaleDirection(this.currentLanguage() ?? 'en'));

  protected toggleMobileNav(): void {
    this.mobileNavOpen.update((open) => !open);
  }

  protected closeMobileNav(): void {
    this.mobileNavOpen.set(false);
  }

  protected toggleDarkMode(): void {
    this.darkMode.update((dark) => !dark);
  }

  protected toggleLangMenu(): void {
    this.langMenuOpen.update((open) => !open);
  }

  protected selectLanguage(cultureName: string): void {
    this.langMenuOpen.set(false);
    this.cultureUrlService.applyLanguageSelection(cultureName);
  }

  protected logout(): void {
    this.authService.logout().subscribe();
  }
}
