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
interface AdminNavItem {
  labelKey: string;
  icon: string;
  link: string;
  permission: string;
}

interface AdminNavGroup {
  groupKey: string;
  items: AdminNavItem[];
}

/** Grouped to match the prototype's sidebar structure (Overview/Platform/Billing/Operations/System),
 *  but ONLY groups that already have at least one real, built page — no placeholder/disabled entries
 *  for features that don't exist yet (Audit Logs, "Feature Flags" as a standalone page — see below).
 *  Add each new group/entry here the same turn its page is build-verified, not before — an empty
 *  group would just be visual noise pointing at nothing. Dashboard ("Overview" group) was built last,
 *  per the backend readiness doc's implementation order — it composes widgets from pages that needed
 *  to exist first (Businesses, Support Tickets), and see admin-dashboard.component.ts's own file
 *  comment for exactly which of the prototype's stat tiles/charts are real vs. skipped.
 *
 *  The "System" group's Users/Roles/Settings/My Profile items point at **stock ABP pages**
 *  (`@abp/ng.identity`/`@abp/ng.setting-management`/`@abp/ng.account`), but nested under `/admin/...`
 *  in app.routes.ts (`/admin/identity/users`, not the packages' own top-level `/identity/users`) so
 *  they render INSIDE this shell instead of Lepton-X's stock SideMenu chrome — first shipped pointing
 *  at the top-level paths, which visibly broke chrome consistency (jumped to a totally different,
 *  unbranded layout on click); fixed by nesting rather than by trying to reskin the stock pages.
 *  route.provider.ts has four `invisible: true` layout-anchor entries (`/admin/identity`, `/admin/
 *  setting-management`, `/admin/account`, `/admin/tenant-management`) purely so `findRoute()`'s
 *  path-walk-up resolves `eLayoutType.empty` for everything under them — real permission checks still
 *  live entirely inside each package's own `createRoutes()` (confirmed by reading each package's
 *  compiled output: `AbpIdentity.Roles`/`.Users`, `authGuard` for Account,
 *  `AbpTenantManagement.Tenants`), unaffected by where they're mounted. Raw ABP Tenant Management
 *  (`/admin/tenant-management/tenants`) is a genuinely DIFFERENT concept from our own "Businesses"
 *  item in the Platform group — that's `BusinessProfile`'s approval workflow; this is the underlying
 *  `Volo.Abp.TenantManagement.Tenant` record itself (connection strings, deactivate/delete) — both
 *  legitimately belong in the nav, not a duplicate of each other.
 *
 *  Deliberately NOT added: a standalone "Feature Flags" nav entry. `FeatureManagementComponent` (from
 *  `@abp/ng.feature-management`) is a **modal**, not a routed page — it needs a `providerKey`/
 *  `providerName` (e.g. a specific tenant) to open against, normally supplied by a "Manage host
 *  features" button on a tenant row in Tenant Management's own list (now present above, but that
 *  button lives inside the stock `TenantsComponent` itself, not something a standalone Eksabli nav
 *  entry could target) — so there's still no real *standalone* place to open that modal from; a bare
 *  nav link with nothing real behind it would be exactly the kind of broken-looking dead end the hard
 *  rules say not to ship. Labels are localization keys — reuses the real, already-shipped resource
 *  keys each stock page's own self-registered menu entry uses (`AbpIdentity::Users`,
 *  `AbpIdentity::Roles`, `AbpSettingManagement::Settings`, `AbpTenantManagement::Tenants`,
 *  `AbpAccount::MyAccount` — read directly from each package's `*-config.mjs`, not guessed) for the
 *  System group, and the app's own `Menu:*`/`AdminPanel:*` keys for everything else. */
const ADMIN_NAV: AdminNavGroup[] = [
  {
    // Host-only (Eksabli.Tenants.View) — listed first to match the prototype's own IA (Dashboard is
    // the landing page for platform staff, see app.routes.ts's bare '/admin' -> 'dashboard' redirect).
    // Doesn't affect business-staff accounts either way — they're filtered out of this group by
    // permission regardless of position, same as every other Host-only group below.
    groupKey: '::AdminPanel:Layout:GroupOverview',
    items: [
      { labelKey: '::AdminPanel:Dashboard:Title', icon: 'fa-gauge-high', link: '/admin/dashboard', permission: 'Eksabli.Tenants.View' },
    ],
  },
  {
    groupKey: '::AdminPanel:Layout:GroupPlatform',
    items: [
      { labelKey: '::Menu:AdminTenants', icon: 'fa-building', link: '/admin/businesses', permission: 'Eksabli.Tenants.View' },
      // Cross-tenant user directory — a genuinely different permission from Tenants.View (see
      // EksabliPermissions.Users' own comment: it exposes contact info across every tenant, audited
      // separately).
      { labelKey: '::Menu:AdminUsers', icon: 'fa-address-book', link: '/admin/users', permission: 'Eksabli.Users.View' },
      { labelKey: '::Menu:Categories', icon: 'fa-tags', link: '/admin/categories', permission: 'Eksabli.Tenants.View' },
    ],
  },
  {
    groupKey: '::AdminPanel:Layout:GroupBilling',
    items: [
      { labelKey: '::Menu:SubscriptionPlans', icon: 'fa-receipt', link: '/admin/plans', permission: 'Eksabli.Tenants.View' },
      {
        labelKey: '::AdminPanel:Subscriptions:Title',
        icon: 'fa-credit-card',
        link: '/admin/subscriptions',
        permission: 'Eksabli.Billing.ManagePlatform',
      },
    ],
  },
  {
    groupKey: '::AdminPanel:Layout:GroupOperations',
    items: [
      {
        labelKey: '::Menu:SupportTickets',
        icon: 'fa-life-ring',
        link: '/admin/support-tickets',
        permission: 'Eksabli.SupportTickets.Manage',
      },
      {
        labelKey: '::AdminPanel:AuditLogs:Title',
        icon: 'fa-list-check',
        link: '/admin/audit-logs',
        permission: 'Eksabli.AuditLogs',
      },
    ],
  },
  {
    groupKey: '::AdminPanel:Layout:GroupSystem',
    items: [
      // Nested under /admin (not the packages' own top-level /identity, /setting-management,
      // /account mounts — see app.routes.ts's comment) so these render inside THIS shell instead of
      // handing off to Lepton-X's stock chrome.
      { labelKey: 'AbpIdentity::Users', icon: 'fa-user', link: '/admin/identity/users', permission: 'AbpIdentity.Users' },
      { labelKey: 'AbpIdentity::Roles', icon: 'fa-user-shield', link: '/admin/identity/roles', permission: 'AbpIdentity.Roles' },
      {
        labelKey: 'AbpSettingManagement::Settings',
        icon: 'fa-cog',
        link: '/admin/setting-management',
        permission: 'SettingManagement.Emailing',
      },
      // No permission gate — matches the stock route's own lack of a requiredPolicy (any
      // authenticated user manages their own profile); an empty string is PermissionService's own
      // "always granted" shape (see `isPolicyGranted`'s `if (!key) return true`), not a placeholder.
      { labelKey: 'AbpAccount::MyAccount', icon: 'fa-id-badge', link: '/admin/account/manage', permission: '' },
      // Raw ABP Tenant Management — DIFFERENT concept from our own "Businesses" item in the Platform
      // group above (that's BusinessProfile's approval workflow; this is the underlying
      // Volo.Abp.TenantManagement.Tenant record itself — connection strings, deactivate/delete).
      // Deliberately gated on the real stock permission (`AbpTenantManagement.Tenants`, defaults to
      // SuperAdmin-only) rather than reusing `Eksabli.Tenants.View` — a viewer who can approve/suspend
      // businesses shouldn't automatically also get raw tenant-record deletion. `fa-server` (not
      // `fa-building`, already used for Businesses) to visually distinguish "infrastructure record"
      // from "business entity" at a glance.
      {
        labelKey: 'AbpTenantManagement::Tenants',
        icon: 'fa-server',
        link: '/admin/tenant-management/tenants',
        permission: 'AbpTenantManagement.Tenants',
      },
    ],
  },
];

@Component({
  selector: 'app-admin-layout',
  // Deliberately NOT OnPush, despite .claude/CLAUDE.md's default — this is a shell wrapping arbitrary
  // routed content via <router-outlet>, including third-party stock ABP pages (Users/Roles/Settings,
  // nested under /admin — see app.routes.ts) that update their own state via plain property
  // assignment (`this.data = res`), not signals. A signal write auto-propagates a dirty-mark up
  // through OnPush ancestors (Angular's own reactivity integration); a plain assignment does not —
  // Angular's CD tree walk hits this component (OnPush, not dirty) and prunes the ENTIRE subtree,
  // including the outlet's content, so the stock page's data silently never repaints until some
  // unrelated click on THIS component's own template (e.g. a nav link's (click) handler) forces it to
  // be checked. Reproduced and root-caused this exact way: Users list loaded correctly on the first
  // visit (network call succeeded, `this.data` updated) but the table stayed empty until a second,
  // unrelated click. Our OWN admin pages (Businesses, Categories, ...) all use signals and are
  // unaffected either way — this only matters because this shell also hosts non-signal descendants it
  // doesn't control.
  templateUrl: './admin-layout.component.html',
  styleUrls: ['./admin-layout.component.scss'],
  imports: [RouterOutlet, RouterLink, RouterLinkActive, LocalizationPipe, NotificationBellComponent],
})
export class AdminLayoutComponent {
  private readonly authService = inject(AuthService);
  private readonly permissionService = inject(PermissionService);
  private readonly configState = inject(ConfigStateService);
  private readonly sessionState = inject(SessionStateService);
  private readonly cultureUrlService = inject(RouteBasedCultureUrlService);

  protected readonly mobileNavOpen = signal(false);
  protected readonly darkMode = signal(false);
  protected readonly langMenuOpen = signal(false);

  /** Per-group permission filter, then drop any group left with zero visible items (e.g. a viewer
   *  without Billing.ManagePlatform shouldn't see an empty "Billing" header). Realm itself is already
   *  handled at the route level (`adminGuard` on the whole `/admin` subtree — see app.routes.ts) —
   *  everyone who reaches this shell at all is a platform admin, no additional per-item realm
   *  filtering is needed here (unlike the shell's own earlier design, back when Customers/Employees
   *  briefly lived inside this same shell). */
  protected readonly navGroups = computed<AdminNavGroup[]>(() =>
    ADMIN_NAV.map((group) => ({
      groupKey: group.groupKey,
      items: group.items.filter((item) => this.permissionService.getGrantedPolicy(item.permission)),
    })).filter((group) => group.items.length > 0),
  );

  protected readonly currentUserName = computed(() => {
    const currentUser = this.configState.getOne('currentUser') as { userName?: string } | undefined;
    return currentUser?.userName ?? 'Admin';
  });

  /** `LanguageInfo[]` from ABP config — same source Lepton-X's own language picker reads
   *  (`configState.getOne('localization').languages`), each with `cultureName`/`displayName`. */
  protected readonly languages = computed(() => {
    const localization = this.configState.getOne('localization') as { languages?: { cultureName: string; displayName: string }[] } | undefined;
    return localization?.languages ?? [];
  });

  protected readonly currentLanguage = toSignal(this.sessionState.getLanguage$(), {
    initialValue: this.sessionState.getLanguage(),
  });

  /** Bound to the shell root so Bootstrap's logical spacing utilities and this component's own
   *  logical CSS properties (see admin-layout.component.scss) flip correctly under Arabic — matches
   *  the `getLocaleDirection()` pattern NEXT_SESSION_PROMPT.md's gotcha #3 documents for this app. */
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
