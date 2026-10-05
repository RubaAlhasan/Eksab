import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { LocalizationPipe, LocalizationService, SessionStateService } from '@abp/ng.core';
import { AdminDashboardService } from '../../proxy/controllers/admin-dashboard.service';
import type {
  AdminActivityItemDto,
  AdminAlertsDto,
  AdminDashboardSummaryDto,
  AdminDashboardTrendPointDto,
  DashboardRangeDto,
  TopBusinessDto,
  TopOfferDto,
} from '../../proxy/dashboards/models';
import { AdminActivityKind } from '../../proxy/dashboards/admin-activity-kind.enum';
import { Currency } from '../../proxy/shared/currency.enum';
import { BarTrendComponent, TrendBar } from '../../shared/components/dashboard/bar-trend.component';
import { KpiTileComponent } from '../../shared/components/dashboard/kpi-tile.component';
import { SegmentedControlComponent, SegmentedOption } from '../../shared/components/dashboard/segmented-control.component';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { LoadingSpinnerComponent } from '../../shared/components/loading-spinner/loading-spinner.component';
import { ErrorStateComponent } from '../../shared/components/error-state/error-state.component';
import {
  addDays,
  amountIn,
  formatAmount,
  formatCount,
  formatIsoDate,
  pickLocalized,
} from '../../shared/utils/dashboard-format.util';

type AdminTrendMetric = 'points' | 'customers' | 'businesses' | 'sales';

const RANGE_OPTIONS: SegmentedOption[] = [
  { value: 7, labelKey: '::Dashboard360:Range:Week' },
  { value: 30, labelKey: '::Dashboard360:Range:Month' },
  { value: 90, labelKey: '::Dashboard360:Range:Quarter' },
];

const DEFAULT_RANGE_DAYS = 30;

const CURRENCY_OPTIONS: SegmentedOption[] = [
  { value: Currency.Syp, labelKey: '::Dashboard360:Currency:Syp' },
  { value: Currency.Usd, labelKey: '::Dashboard360:Currency:Usd' },
];

const METRIC_OPTIONS: SegmentedOption[] = [
  { value: 'points', labelKey: '::AdminPanel:Dashboard360:Metric:Points' },
  { value: 'customers', labelKey: '::AdminPanel:Dashboard360:Metric:Customers' },
  { value: 'businesses', labelKey: '::AdminPanel:Dashboard360:Metric:Businesses' },
  { value: 'sales', labelKey: '::AdminPanel:Dashboard360:Metric:Sales' },
];

/**
 * Admin Portal > Dashboard, rebuilt as Dashboard 360. The platform view leads with health and growth: who is approved,
 * who is waiting, what is moving, and what needs a human. Money is shown one currency at a time, and only when the
 * caller holds the billing permission. Every section degrades on its own, so a missing permission removes one tile or
 * one alert, not the page.
 */
@Component({
  selector: 'app-admin-dashboard',
  templateUrl: './admin-dashboard.component.html',
  styleUrls: ['./admin-dashboard.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    RouterLink,
    LocalizationPipe,
    PageHeaderComponent,
    LoadingSpinnerComponent,
    ErrorStateComponent,
    KpiTileComponent,
    BarTrendComponent,
    SegmentedControlComponent,
  ],
})
export class AdminDashboardComponent implements OnInit {
  private readonly dashboardService = inject(AdminDashboardService);
  private readonly localization = inject(LocalizationService);
  private readonly session = inject(SessionStateService);

  protected readonly rangeOptions = RANGE_OPTIONS;
  protected readonly currencyOptions = CURRENCY_OPTIONS;
  protected readonly metricOptions = METRIC_OPTIONS;
  protected readonly AdminActivityKind = AdminActivityKind;

  protected readonly isLoading = signal(true);
  protected readonly isRefreshing = signal(false);
  protected readonly loadFailed = signal(false);

  protected readonly rangeDays = signal(DEFAULT_RANGE_DAYS);
  protected readonly currency = signal<Currency>(Currency.Syp);
  protected readonly metric = signal<AdminTrendMetric>('points');

  protected readonly summary = signal<AdminDashboardSummaryDto | null>(null);
  protected readonly trends = signal<AdminDashboardTrendPointDto[]>([]);
  protected readonly topBusinesses = signal<TopBusinessDto[]>([]);
  protected readonly topOffers = signal<TopOfferDto[]>([]);
  protected readonly alerts = signal<AdminAlertsDto | null>(null);
  protected readonly activity = signal<AdminActivityItemDto[]>([]);

  // The window's dates are UTC for the platform. The server's own "today" anchors the range, so the browser's clock
  // never shifts a day.
  private readonly platformToday = computed(() => {
    const trendDates = this.trends();
    return trendDates.length > 0 ? trendDates[trendDates.length - 1].date ?? null : null;
  });

  // The money tiles and the sales metric are only present when the server returned them (the caller holds billing).
  protected readonly canViewRevenue = computed(() => this.summary()?.recordedValue != null);

  protected readonly currencyLabel = computed(() =>
    this.localization.instant(this.currency() === Currency.Syp ? '::Dashboard360:Currency:Syp' : '::Dashboard360:Currency:Usd'),
  );

  protected readonly salesTotal = computed(() => amountIn(this.summary()?.recordedValue, this.currency()));

  protected readonly businessesCaption = computed(() => {
    const s = this.summary();
    return this.localization.instant(
      '::AdminPanel:Dashboard360:Caption:BusinessesFormat',
      formatCount(s?.businessesApproved ?? 0),
      formatCount(s?.businessesPending ?? 0),
    );
  });

  protected readonly customersCaption = computed(() =>
    this.localization.instant('::AdminPanel:Dashboard360:Caption:NewCustomersFormat', formatCount(this.summary()?.newCustomers ?? 0)),
  );

  protected readonly pointsCaption = computed(() =>
    this.localization.instant(
      '::AdminPanel:Dashboard360:Caption:PointsFormat',
      formatCount(this.summary()?.pointsRedeemed ?? 0),
      formatCount(this.summary()?.transactions ?? 0),
    ),
  );

  protected readonly salesCaption = computed(() =>
    this.localization.instant('::AdminPanel:Dashboard360:Caption:BuyNowFormat', formatCount(this.summary()?.buyNowSales ?? 0)),
  );

  protected readonly trendBars = computed<TrendBar[]>(() => {
    const points = this.trends();
    const metric = this.metric();
    const currency = this.currency();
    const labelEvery = Math.max(1, Math.ceil(points.length / 7));

    return points.map((point, index) => {
      const date = point.date ?? '';
      const day = formatIsoDate(date, { day: 'numeric', month: 'short' });
      const label = index % labelEvery === 0 ? day : '';
      const value = this.metricValue(point, metric, currency);
      const shown = metric === 'sales' ? `${formatAmount(value, currency)} ${this.currencyLabel()}` : formatCount(value);
      return { label, value, tooltip: `${day}: ${shown}` };
    });
  });

  protected readonly trendTotal = computed(() => {
    const metric = this.metric();
    const currency = this.currency();
    return this.trends().reduce((sum, point) => sum + this.metricValue(point, metric, currency), 0);
  });

  // "Sales" is money, so the option only exists for a caller who can see revenue.
  protected readonly visibleMetricOptions = computed(() =>
    METRIC_OPTIONS.filter((option) => option.value !== 'sales' || this.canViewRevenue()),
  );

  protected readonly alertChips = computed(() => {
    const a = this.alerts();
    if (!a) return [];
    const chips: { key: string; count: number; link: string; tone: 'warning' | 'danger' | 'info' }[] = [
      { key: '::AdminPanel:Dashboard360:Alert:Pending', count: a.pendingApprovals ?? 0, link: '/admin/businesses', tone: 'warning' },
      { key: '::AdminPanel:Dashboard360:Alert:Suspended', count: a.suspendedBusinesses ?? 0, link: '/admin/businesses', tone: 'danger' },
      { key: '::AdminPanel:Dashboard360:Alert:LowStock', count: a.lowStockRewards ?? 0, link: '/admin/businesses', tone: 'warning' },
    ];
    if (a.pastDueSubscriptions != null) {
      chips.push({ key: '::AdminPanel:Dashboard360:Alert:PastDue', count: a.pastDueSubscriptions, link: '/admin/subscriptions', tone: 'danger' });
    }
    if (a.openSupportTickets != null) {
      chips.push({ key: '::AdminPanel:Dashboard360:Alert:Tickets', count: a.openSupportTickets, link: '/admin/support-tickets', tone: 'info' });
    }
    return chips;
  });

  protected readonly maxTopPoints = computed(() => Math.max(1, ...this.topBusinesses().map((b) => b.pointsIssued ?? 0)));

  ngOnInit(): void {
    this.load();
  }

  protected setRange(days: string | number): void {
    this.rangeDays.set(Number(days));
    this.isRefreshing.set(true);
    this.load();
  }

  protected setCurrency(value: string | number): void {
    this.currency.set(Number(value) as Currency);
  }

  protected setMetric(value: string | number): void {
    this.metric.set(value as AdminTrendMetric);
  }

  protected retry(): void {
    this.isLoading.set(true);
    this.load();
  }

  protected countText(value: number | null | undefined): string {
    return formatCount(value ?? 0);
  }

  protected formatMoney(amount: number): string {
    return formatAmount(amount, this.currency());
  }

  protected shareOfTop(points: number | undefined): number {
    return Math.round(((points ?? 0) / this.maxTopPoints()) * 100);
  }

  protected formatOfferValue(offer: TopOfferDto): string {
    const amounts = offer.completedValue ?? [];
    if (amounts.length === 0) {
      return '—';
    }
    return amounts.map((a) => `${formatAmount(a.amount ?? 0, a.currency ?? Currency.Syp)} ${this.currencyCode(a.currency)}`).join(' · ');
  }

  protected offerTitle(offer: TopOfferDto): string {
    return pickLocalized(offer.titleAr, offer.titleEn, this.session.getLanguage());
  }

  protected activityKey(item: AdminActivityItemDto): string {
    switch (item.kind) {
      case AdminActivityKind.BusinessRegistered:
        return '::AdminPanel:Dashboard360:Activity:BusinessRegistered';
      case AdminActivityKind.CustomerJoined:
        return '::AdminPanel:Dashboard360:Activity:CustomerJoined';
      default:
        return '::AdminPanel:Dashboard360:Activity:TicketOpened';
    }
  }

  protected activityIcon(item: AdminActivityItemDto): string {
    switch (item.kind) {
      case AdminActivityKind.BusinessRegistered:
        return 'fa-store';
      case AdminActivityKind.CustomerJoined:
        return 'fa-user-plus';
      default:
        return 'fa-headset';
    }
  }

  private metricValue(point: AdminDashboardTrendPointDto, metric: AdminTrendMetric, currency: Currency): number {
    switch (metric) {
      case 'customers':
        return point.newCustomers ?? 0;
      case 'businesses':
        return point.newBusinesses ?? 0;
      case 'sales':
        return amountIn(point.recordedValue, currency);
      default:
        return point.pointsIssued ?? 0;
    }
  }

  private currencyCode(currency: Currency | undefined): string {
    return this.localization.instant(
      currency === Currency.Usd ? '::Dashboard360:Currency:Usd' : '::Dashboard360:Currency:Syp',
    );
  }

  // Before the first response there is no server "today" yet, so the first call takes the server's default window.
  private requestRange(): DashboardRangeDto {
    const today = this.platformToday();
    if (!today) {
      return { from: undefined, to: undefined };
    }
    return { from: addDays(today, 1 - this.rangeDays()), to: today };
  }

  private load(): void {
    this.loadFailed.set(false);
    const range = this.requestRange();

    forkJoin({
      summary: this.dashboardService.getSummary(range),
      trends: this.dashboardService.getTrends(range),
      topBusinesses: this.dashboardService.getTopBusinesses(range),
      topOffers: this.dashboardService.getTopOffers(range),
      alerts: this.dashboardService.getAlerts(),
      activity: this.dashboardService.getActivity(),
    }).subscribe({
      next: (result) => {
        this.summary.set(result.summary);
        this.trends.set(result.trends);
        this.topBusinesses.set(result.topBusinesses);
        this.topOffers.set(result.topOffers);
        this.alerts.set(result.alerts);
        this.activity.set(result.activity);
        this.isLoading.set(false);
        this.isRefreshing.set(false);
      },
      error: () => {
        this.isLoading.set(false);
        this.isRefreshing.set(false);
        this.loadFailed.set(true);
      },
    });
  }
}
