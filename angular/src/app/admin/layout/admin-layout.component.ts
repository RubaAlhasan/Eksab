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

/** Flat for now (Businesses, Categories, Plans) — Dashboard is deliberately not built yet per the
 *  backend readiness doc's implementation order (it composes widgets from pages that need to exist
 *  first). Add entries here as each Admin feature ships. Labels are localization keys, not literal
 *  text — reuses the same `Menu:*` keys each page's own sidebar/menu entry already uses in
 *  route.provider.ts, so there's one translated string per feature, not two. */
const ADMIN_NAV: AdminNavItem[] = [
  { labelKey: '::Menu:AdminTenants', icon: 'fa-building', link: '/admin/businesses', permission: 'Eksabli.Tenants.View' },
  { labelKey: '::Menu:Categories', icon: 'fa-tags', link: '/admin/categories', permission: 'Eksabli.Tenants.View' },
  { labelKey: '::Menu:SubscriptionPlans', icon: 'fa-receipt', link: '/admin/plans', permission: 'Eksabli.Tenants.View' },
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

  protected readonly navItems = computed(() =>
    ADMIN_NAV.filter((item) => this.permissionService.getGrantedPolicy(item.permission)),
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
