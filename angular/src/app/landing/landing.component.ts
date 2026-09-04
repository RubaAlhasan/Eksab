import {
  AuthService,
  LocalizationPipe,
  LocalizationService,
  RouteBasedCultureUrlService,
  SessionStateService,
  getLocaleDirection,
} from '@abp/ng.core';
import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  Renderer2,
  afterNextRender,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { Meta, Title } from '@angular/platform-browser';

interface WalletCard {
  tenant: 't1' | 't2' | 't3';
  initials: string;
  name: string;
  balance: number;
  stamped?: boolean;
}

interface Step {
  number: string;
  titleKey: string;
  bodyKey: string;
}

interface Feature {
  titleKey: string;
  bodyKey: string;
}

interface PricingTier {
  featured: boolean;
  tagKey?: string;
  nameKey: string;
  descKey: string;
  itemKeys: string[];
}

@Component({
  selector: 'app-landing',
  templateUrl: './landing.component.html',
  styleUrls: ['./landing.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [LocalizationPipe],
  host: {
    '[attr.dir]': 'dir()',
  },
})
export class LandingComponent {
  private readonly authService = inject(AuthService);
  private readonly sessionState = inject(SessionStateService);
  private readonly routeBasedCultureUrl = inject(RouteBasedCultureUrlService);
  private readonly renderer = inject(Renderer2);
  private readonly elementRef: ElementRef<HTMLElement> = inject(ElementRef);
  private readonly localization = inject(LocalizationService);
  private readonly titleService = inject(Title);
  private readonly metaService = inject(Meta);

  /** Reflects ABP's actual active culture (session state), not component-local UI state. */
  readonly currentLang = toSignal(this.sessionState.getLanguage$(), {
    initialValue: this.sessionState.getLanguage() ?? 'en',
  });
  readonly dir = computed(() => getLocaleDirection(this.currentLang() ?? 'en'));

  /** Toggled once the page scrolls past the hero — gives the sticky header a slightly stronger
   *  elevation once there's content "under" it, a common affordance on modern marketing sites. */
  readonly scrolled = signal(false);

  /** Mobile header collapse — below the breakpoint where `.main-nav` + `.header-actions` no longer
   *  fit alongside the wordmark in one row (see landing.component.scss's `.menu-toggle`/`.mobile-menu`
   *  rules), the same nav/lang/login/CTA content moves into this toggled panel instead of overflowing.
   *  Same signal-based toggle shape as AdminLayoutComponent/BusinessLayoutComponent's own
   *  `mobileNavOpen`, kept local here since this page has no shared shell to hoist it into. */
  readonly mobileMenuOpen = signal(false);

  readonly walletCards: WalletCard[] = [
    { tenant: 't3', initials: 'CC', name: 'Crust & Co.', balance: 210 },
    { tenant: 't2', initials: 'FL', name: 'FitLine Sports', balance: 120 },
    { tenant: 't1', initials: 'BB', name: 'Bloom & Brew', balance: 450, stamped: true },
  ];

  readonly steps: Step[] = [
    { number: '01', titleKey: '::Landing:Step1Title', bodyKey: '::Landing:Step1Body' },
    { number: '02', titleKey: '::Landing:Step2Title', bodyKey: '::Landing:Step2Body' },
    { number: '03', titleKey: '::Landing:Step3Title', bodyKey: '::Landing:Step3Body' },
  ];

  readonly features: Feature[] = [
    { titleKey: '::Landing:Feature1Title', bodyKey: '::Landing:Feature1Body' },
    { titleKey: '::Landing:Feature2Title', bodyKey: '::Landing:Feature2Body' },
    { titleKey: '::Landing:Feature3Title', bodyKey: '::Landing:Feature3Body' },
    { titleKey: '::Landing:Feature4Title', bodyKey: '::Landing:Feature4Body' },
  ];

  readonly ledgerFactKeys: string[] = [
    '::Landing:LedgerFact1',
    '::Landing:LedgerFact2',
    '::Landing:LedgerFact3',
  ];

  readonly pricingTiers: PricingTier[] = [
    {
      featured: false,
      nameKey: '::Landing:TierStarterName',
      descKey: '::Landing:TierStarterDesc',
      itemKeys: ['::Landing:TierStarterItem1', '::Landing:TierStarterItem2', '::Landing:TierStarterItem3'],
    },
    {
      featured: true,
      tagKey: '::Landing:TierGrowthTag',
      nameKey: '::Landing:TierGrowthName',
      descKey: '::Landing:TierGrowthDesc',
      itemKeys: ['::Landing:TierGrowthItem1', '::Landing:TierGrowthItem2', '::Landing:TierGrowthItem3'],
    },
    {
      featured: false,
      nameKey: '::Landing:TierEnterpriseName',
      descKey: '::Landing:TierEnterpriseDesc',
      itemKeys: [
        '::Landing:TierEnterpriseItem1',
        '::Landing:TierEnterpriseItem2',
        '::Landing:TierEnterpriseItem3',
      ],
    },
  ];

  constructor() {
    afterNextRender(() => {
      this.setupScrollReveal();
      this.setupHeaderScrollState();
    });
    // Re-runs whenever the ABP session culture flips (EN/AR toggle), keeping <title>, meta tags,
    // <html lang>, and the JSON-LD block in sync with what's actually on screen.
    effect(() => this.updateSeo(this.currentLang() ?? 'en'));
  }

  get hasLoggedIn(): boolean {
    return this.authService.isAuthenticated;
  }

  setLang(lang: 'en' | 'ar'): void {
    this.routeBasedCultureUrl.applyLanguageSelection(lang);
  }

  toggleMobileMenu(): void {
    this.mobileMenuOpen.update((open) => !open);
  }

  closeMobileMenu(): void {
    this.mobileMenuOpen.set(false);
  }

  login(): void {
    this.authService.navigateToLogin();
  }

  private setupScrollReveal(): void {
    const root = this.elementRef.nativeElement;
    const targets = Array.from(root.querySelectorAll<HTMLElement>('[data-reveal]'));
    const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

    if (reduceMotion || !('IntersectionObserver' in window)) {
      targets.forEach(el => this.renderer.addClass(el, 'in-view'));
      return;
    }

    const observer = new IntersectionObserver(
      entries => {
        entries.forEach(entry => {
          if (entry.isIntersecting) {
            this.renderer.addClass(entry.target, 'in-view');
            observer.unobserve(entry.target);
          }
        });
      },
      { threshold: 0.15 },
    );

    targets.forEach(el => observer.observe(el));
  }

  /** Adds a `.scrolled` affordance to the sticky header once the page has moved past the hero. */
  private setupHeaderScrollState(): void {
    const onScroll = () => this.scrolled.set(window.scrollY > 8);
    onScroll();
    this.renderer.listen('window', 'scroll', onScroll);
  }

  /** Keeps `<title>`, meta description/Open Graph/Twitter tags, `<html lang>`, the canonical link,
   *  and the JSON-LD block aligned with the currently active locale. There's no SSR in this app, so
   *  the DOM-touching parts are guarded rather than deferred — see `setupScrollReveal` above for the
   *  same `window`-availability caution applied elsewhere in this component. */
  private updateSeo(lang: string): void {
    const title = this.localization.instant('::Landing:MetaTitle');
    const description = this.localization.instant('::Landing:MetaDescription');

    this.titleService.setTitle(title);
    this.metaService.updateTag({ name: 'description', content: description });
    this.metaService.updateTag({ property: 'og:title', content: title });
    this.metaService.updateTag({ property: 'og:description', content: description });
    this.metaService.updateTag({ property: 'og:locale', content: lang === 'ar' ? 'ar_SA' : 'en_US' });
    this.metaService.updateTag({ name: 'twitter:title', content: title });
    this.metaService.updateTag({ name: 'twitter:description', content: description });

    if (typeof window === 'undefined') return;

    document.documentElement.lang = lang;

    const pageUrl = `${window.location.origin}${window.location.pathname}`;
    this.metaService.updateTag({ property: 'og:url', content: pageUrl });
    this.updateCanonicalLink(pageUrl);
    this.updateStructuredData(pageUrl, title, description);
  }

  private updateCanonicalLink(href: string): void {
    let link = document.head.querySelector<HTMLLinkElement>('link[rel="canonical"]');
    if (!link) {
      link = this.renderer.createElement('link');
      this.renderer.setAttribute(link, 'rel', 'canonical');
      this.renderer.appendChild(document.head, link);
    }
    this.renderer.setAttribute(link, 'href', href);
  }

  /** Organization + WebSite structured data — deliberately free of ratings/reviews/pricing, since
   *  none of those are published facts (see the pricing section's own "illustrative tiers" copy). */
  private updateStructuredData(pageUrl: string, title: string, description: string): void {
    const structuredData = {
      '@context': 'https://schema.org',
      '@graph': [
        {
          '@type': 'Organization',
          name: 'Eksabli',
          url: pageUrl,
          description,
        },
        {
          '@type': 'WebSite',
          name: title,
          url: pageUrl,
          description,
          inLanguage: this.currentLang() === 'ar' ? 'ar' : 'en',
        },
      ],
    };

    let script = document.getElementById('landing-structured-data') as HTMLScriptElement | null;
    if (!script) {
      script = this.renderer.createElement('script');
      this.renderer.setAttribute(script, 'type', 'application/ld+json');
      this.renderer.setAttribute(script, 'id', 'landing-structured-data');
      this.renderer.appendChild(document.head, script);
    }
    script.textContent = JSON.stringify(structuredData);
  }
}
