import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
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
 *  for features that don't exist yet (Feature Flags, Audit Logs, stock ABP Users/Roles/Settings). Add
 *  each new group/entry here the same turn its page is build-verified, not before — an empty "System"
 *  section would just be visual noise pointing at nothing. Dashboard (would-be "Overview" group) is
 *  deliberately not built yet per the backend readiness doc's implementation order (it composes
 *  widgets from pages that need to exist first). Labels are localization keys, not literal text —
 *  reuses the same `Menu:*` keys each page's own sidebar/menu entry already uses in
 *  route.provider.ts, so there's one translated string per feature, not two. */
const ADMIN_NAV: AdminNavGroup[] = [
  {
    groupKey: '::AdminPanel:Layout:GroupPlatform',
    items: [
      { labelKey: '::Menu:AdminTenants', icon: 'fa-building', link: '/admin/businesses', permission: 'Eksabli.Tenants.View' },
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
    ],
  },
];

@Component({
  selector: 'app-admin-layout',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './admin-layout.component.html',
  styleUrls: ['./admin-layout.component.scss'],
  imports: [RouterOutlet, RouterLink, RouterLinkActive, LocalizationPipe],
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

  /** Per-group permission filter, then drop any group left with zero visible items (e.g. a
   *  tenant-realm-scoped admin who lacks Billing.ManagePlatform shouldn't see an empty "Billing"
   *  header). */
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
