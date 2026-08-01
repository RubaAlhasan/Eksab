import {
  AuthService,
  LocalizationPipe,
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
  inject,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';

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

  /** Reflects ABP's actual active culture (session state), not component-local UI state. */
  readonly currentLang = toSignal(this.sessionState.getLanguage$(), {
    initialValue: this.sessionState.getLanguage() ?? 'en',
  });
  readonly dir = computed(() => getLocaleDirection(this.currentLang() ?? 'en'));

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
    afterNextRender(() => this.setupScrollReveal());
  }

  get hasLoggedIn(): boolean {
    return this.authService.isAuthenticated;
  }

  setLang(lang: 'en' | 'ar'): void {
    this.routeBasedCultureUrl.applyLanguageSelection(lang);
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
}
