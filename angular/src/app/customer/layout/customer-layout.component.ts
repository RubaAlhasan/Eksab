import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { Location } from '@angular/common';
import { NavigationEnd, Router, RouterLink, RouterOutlet } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { filter } from 'rxjs';
import { LocalizationPipe, SessionStateService, getLocaleDirection } from '@abp/ng.core';
import { NotificationHubService } from '../../shared/services/notification-hub.service';
import { CustomerThemeService } from '../../shared/services/customer-theme.service';

type CustomerTab = 'home' | 'search' | 'alerts' | 'profile' | null;

/**
 * Customer app shell — mounted at `/customer`, gated only by `authGuard` (same as the bare `/home`
 * route this replaces; there's no dedicated "customer realm" guard anywhere in this app, matching the
 * existing accepted gap in `business.guard.ts`/`admin.guard.ts`).
 *
 * Deliberately mobile-app-first (fixed bottom tab bar), not a dashboard sidebar like
 * `AdminLayoutComponent`/`BusinessLayoutComponent` — this is the whole point of building it (a web
 * substitute for members without the native app) — but reuses their same `--eks-*` design tokens and
 * `RouterOutlet`-based shell shape. OnPush is safe here (unlike those two shells, which stay non-OnPush
 * only because they host non-signal stock-ABP descendants like Identity/Setting Management) since this
 * subtree hosts only our own signal-based pages.
 *
 * Bottom nav has four tabs (Home/Search/Alerts/Profile) — a standalone Wallet tab is deliberately not
 * here yet: Home already covers "which businesses have I joined" (the wallet grid), so a separate Wallet
 * tab would just be a duplicate list; this app's own convention (see `business-layout.component.ts`'s
 * `NAV` comment) is "only real, already-built pages — no placeholder/disabled entries," which cuts the
 * other way here — don't add a tab for content that's already shown elsewhere.
 */
@Component({
  selector: 'app-customer-layout',
  templateUrl: './customer-layout.component.html',
  styleUrls: ['./customer-layout.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterOutlet, RouterLink, LocalizationPipe],
})
export class CustomerLayoutComponent {
  private readonly router = inject(Router);
  private readonly location = inject(Location);
  private readonly sessionState = inject(SessionStateService);
  protected readonly hub = inject(NotificationHubService);

  // See CustomerThemeService's own comment for why this lives in a shared service rather than a local
  // signal like BusinessLayoutComponent/AdminLayoutComponent use — their toggle button sits in their
  // own topbar; this shell's topbar only appears on drill-in pages, so the control lives on Settings
  // (a routed child) instead, which needs to reach the shell's own `[class.dark]` binding.
  protected readonly theme = inject(CustomerThemeService);

  constructor() {
    this.hub.connect();
  }

  // Same `getLocaleDirection()` + `SessionStateService.getLanguage$()` pattern
  // business-layout.component.ts already uses — this shell was simply missed when it was first built,
  // so switching to Arabic (Settings' own language list, which already works) left the customer app's
  // text right-to-left inside a layout that stayed visually left-to-right. Bound as `[attr.dir]` on the
  // shell root in the template; the two directional icon glyphs the shell can't reach through view
  // encapsulation (a routed child page's own `fa-arrow-left`/`fa-chevron-right`) are mirrored globally
  // in styles.scss instead — see that file's own comment for why.
  protected readonly currentLanguage = toSignal(this.sessionState.getLanguage$(), {
    initialValue: this.sessionState.getLanguage(),
  });

  protected readonly direction = computed(() => getLocaleDirection(this.currentLanguage() ?? 'en'));

  private readonly navigationEnd = toSignal(
    this.router.events.pipe(filter((event): event is NavigationEnd => event instanceof NavigationEnd)),
    { initialValue: null },
  );

  private readonly currentUrl = computed(() => this.navigationEnd()?.urlAfterRedirects ?? this.router.url);

  // Deepest activated route's own `data.titleKey` (set per child route in app.routes.ts) — the shell
  // doesn't know each page's title itself, it just reads what the route declares.
  protected readonly pageTitleKey = computed<string | null>(() => {
    this.navigationEnd();
    let route = this.router.routerState.snapshot.root;
    while (route.firstChild) route = route.firstChild;
    return (route.data['titleKey'] as string | undefined) ?? null;
  });

  // Home covers everything "about your wallet" plus the campaign feed (points detail, transactions,
  // rewards catalog/details, the wallet QR, active campaigns — all reached from Home); Search covers
  // discovery (search/nearby + store details — a store can also be reached from Favorites under Profile,
  // but Search is its primary path so it's grouped there); Alerts is the notifications inbox; Profile
  // covers the account hub, My Coupons, Favorites, Refer a Friend, Birthday Rewards, and Settings. Redeem hangs off none of
  // them: the nav is hidden there entirely (see showBottomNav below), matching the one prototype screen
  // (redeem-reward.html) that ships genuinely nav-less rather than just JS-hidden.
  private static readonly HOME_PREFIXES = ['/customer/home', '/customer/wallet', '/customer/qr', '/customer/campaigns'];
  private static readonly SEARCH_PREFIXES = ['/customer/search', '/customer/store'];
  private static readonly ALERTS_PREFIXES = ['/customer/alerts'];
  private static readonly PROFILE_PREFIXES = [
    '/customer/profile',
    '/customer/coupons',
    '/customer/favorites',
    '/customer/referral',
    '/customer/birthday-rewards',
    '/customer/settings',
  ];

  protected readonly activeTab = computed<CustomerTab>(() => {
    const url = this.currentUrl();
    const matches = (prefixes: string[]) => prefixes.some(p => url === p || url.startsWith(p + '/'));
    if (matches(CustomerLayoutComponent.HOME_PREFIXES)) return 'home';
    if (matches(CustomerLayoutComponent.SEARCH_PREFIXES)) return 'search';
    if (matches(CustomerLayoutComponent.ALERTS_PREFIXES)) return 'alerts';
    if (matches(CustomerLayoutComponent.PROFILE_PREFIXES)) return 'profile';
    return null;
  });

  protected readonly showBottomNav = computed(() => !this.currentUrl().startsWith('/customer/redeem'));

  // A root tab page (Home/Search/Alerts/Profile) has no back button; every drill-in does.
  private static readonly ROOT_TAB_URLS = ['/customer/home', '/customer/search', '/customer/alerts', '/customer/profile'];
  protected readonly showBackButton = computed(
    () => !CustomerLayoutComponent.ROOT_TAB_URLS.includes(this.currentUrl()),
  );

  protected goBack(): void {
    this.location.back();
  }
}
