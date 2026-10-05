import { DatePipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { LocalizationPipe, LocalizationService, SessionStateService } from '@abp/ng.core';
import { ReportsService } from '../../proxy/controllers/reports.service';
import { BusinessDashboardService } from '../../proxy/controllers/business-dashboard.service';
import type {
  BusinessDashboardSummaryDto,
  BusinessDashboardTrendPointDto,
  BusinessInsightDto,
  BusinessOfferPerformanceDto,
  DashboardRangeDto,
  PeakHourCellDto,
} from '../../proxy/dashboards/models';
import { BusinessInsightKind } from '../../proxy/dashboards/business-insight-kind.enum';
import { DashboardInsightSeverity } from '../../proxy/dashboards/dashboard-insight-severity.enum';
import type { TransactionListItemDto } from '../../proxy/reports/models';
import { SmartOfferStatus } from '../../proxy/smart-offers/smart-offer-status.enum';
import { Currency } from '../../proxy/shared/currency.enum';
import { ActivityHeatmapComponent, ActivityCell } from '../../shared/components/dashboard/activity-heatmap.component';
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
  formatPercent,
  formatRatio,
  pad2,
  pickLocalized,
} from '../../shared/utils/dashboard-format.util';

type TrendMetric = 'points' | 'sales';

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
  { value: 'points', labelKey: '::Dashboard360:Metric:Points' },
  { value: 'sales', labelKey: '::Dashboard360:Metric:Sales' },
];

/**
 * Business Portal > Dashboard, rebuilt as Dashboard 360. It answers two questions in order: how today is going, and what
 * to do about it. Every number is the business's own, read on its own clock (BusinessProfile.TimeZoneId), and every
 * money figure is shown for one currency at a time. The rules behind the insights run on the server, so this page only
 * turns facts into sentences.
 */
@Component({
  selector: 'app-business-dashboard',
  templateUrl: './business-dashboard.component.html',
  styleUrls: ['./business-dashboard.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    DecimalPipe,
    RouterLink,
    LocalizationPipe,
    PageHeaderComponent,
    LoadingSpinnerComponent,
    ErrorStateComponent,
    KpiTileComponent,
    BarTrendComponent,
    ActivityHeatmapComponent,
    SegmentedControlComponent,
  ],
})
export class BusinessDashboardComponent implements OnInit {
  private readonly dashboardService = inject(BusinessDashboardService);
  private readonly reportsService = inject(ReportsService);
  private readonly localization = inject(LocalizationService);
  private readonly session = inject(SessionStateService);

  protected readonly rangeOptions = RANGE_OPTIONS;
  protected readonly currencyOptions = CURRENCY_OPTIONS;
  protected readonly metricOptions = METRIC_OPTIONS;
  protected readonly Currency = Currency;
  protected readonly SmartOfferStatus = SmartOfferStatus;

  protected readonly isLoading = signal(true);
  protected readonly isRefreshing = signal(false);
  protected readonly loadFailed = signal(false);

  protected readonly rangeDays = signal(DEFAULT_RANGE_DAYS);
  protected readonly currency = signal<Currency>(Currency.Syp);
  protected readonly metric = signal<TrendMetric>('points');

  protected readonly summary = signal<BusinessDashboardSummaryDto | null>(null);
  protected readonly trends = signal<BusinessDashboardTrendPointDto[]>([]);
  protected readonly peakHours = signal<PeakHourCellDto[]>([]);
  protected readonly offers = signal<BusinessOfferPerformanceDto[]>([]);
  protected readonly insights = signal<BusinessInsightDto[]>([]);
  protected readonly recentTransactions = signal<TransactionListItemDto[]>([]);

  // The server's own "today" for this business. Every window is anchored to it, so the browser's clock never decides
  // which day a sale belongs to.
  private readonly businessToday = computed(() => this.summary()?.today ?? null);

  protected readonly currencyLabel = computed(() =>
    this.localization.instant(this.currency() === Currency.Syp ? '::Dashboard360:Currency:Syp' : '::Dashboard360:Currency:Usd'),
  );

  protected readonly todayLabel = computed(() => {
    const today = this.businessToday();
    return today ? formatIsoDate(today, { weekday: 'long', day: 'numeric', month: 'long' }) : '';
  });

  protected readonly todaySales = computed(() =>
    amountIn(this.summary()?.todaySummary?.recordedValue, this.currency()),
  );

  protected readonly salesTotal = computed(() => amountIn(this.summary()?.recordedValue, this.currency()));

  protected readonly membersCaption = computed(() => {
    const s = this.summary();
    return this.localization.instant(
      '::Dashboard360:Caption:MembersFormat',
      formatCount(s?.newMembers ?? 0),
      formatCount(s?.returningMembers ?? 0),
    );
  });

  protected readonly pointsCaption = computed(() => {
    const s = this.summary();
    return this.localization.instant(
      '::Dashboard360:Caption:RedeemedFormat',
      formatCount(s?.pointsRedeemed ?? 0),
      formatRatio(s?.redemptionRate),
    );
  });

  protected readonly pendingCaption = computed(() =>
    this.localization.instant('::Dashboard360:Caption:PendingFormat', formatCount(this.summary()?.pendingBuyNowOrders ?? 0)),
  );

  protected readonly trendLabel = computed(() => this.localization.instant('::Dashboard360:Trend:ChartLabel'));

  protected readonly coverageCaption = computed(() => {
    const pct = this.summary()?.valueCoveragePercent;
    return pct == null
      ? this.localization.instant('::Dashboard360:Caption:NoPurchases')
      : this.localization.instant('::Dashboard360:Caption:CoverageFormat', formatPercent(pct));
  });

  protected readonly trendBars = computed<TrendBar[]>(() => {
    const points = this.trends();
    const metric = this.metric();
    const currency = this.currency();
    // With 90 days there is no room for a label on every column, so only every Nth one is labelled.
    const labelEvery = Math.max(1, Math.ceil(points.length / 7));

    return points.map((point, index) => {
      const date = point.date ?? '';
      const label = index % labelEvery === 0 ? formatIsoDate(date, { day: 'numeric', month: 'short' }) : '';
      if (metric === 'points') {
        const value = point.pointsIssued ?? 0;
        return { label, value, tooltip: `${formatIsoDate(date, { day: 'numeric', month: 'short' })}: ${formatCount(value)}` };
      }
      const value = amountIn(point.recordedValue, currency);
      return {
        label,
        value,
        tooltip: `${formatIsoDate(date, { day: 'numeric', month: 'short' })}: ${formatAmount(value, currency)} ${this.currencyLabel()}`,
      };
    });
  });

  protected readonly trendTotal = computed(() => {
    if (this.metric() === 'points') {
      return this.trends().reduce((sum, p) => sum + (p.pointsIssued ?? 0), 0);
    }
    return this.trends().reduce((sum, p) => sum + amountIn(p.recordedValue, this.currency()), 0);
  });

  protected readonly heatmapCells = computed<ActivityCell[]>(() =>
    this.peakHours().map((cell) => ({
      dayOfWeek: cell.dayOfWeek ?? 0,
      hour: cell.hour ?? 0,
      activity: cell.activity ?? 0,
    })),
  );

  protected readonly dayLabels = computed(() =>
    Array.from({ length: 7 }, (_, i) =>
      // 2024-01-07 was a Sunday, so the loop runs Sunday to Saturday, matching System.DayOfWeek.
      new Date(2024, 0, 7 + i).toLocaleDateString(undefined, { weekday: 'short' }),
    ),
  );

  protected readonly heatmapLabel = computed(() => this.localization.instant('::Dashboard360:Peak:ChartLabel'));

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
    this.metric.set(value as TrendMetric);
  }

  protected retry(): void {
    this.isLoading.set(true);
    this.load();
  }

  protected formatMoney(amount: number): string {
    return formatAmount(amount, this.currency());
  }

  protected countText(value: number | null | undefined): string {
    return formatCount(value ?? 0);
  }

  protected ratioText(value: number | null | undefined): string {
    return formatRatio(value);
  }

  protected formatOfferSales(offer: BusinessOfferPerformanceDto): string {
    const amounts = offer.completedValue ?? [];
    if (amounts.length === 0) {
      return '—';
    }
    return amounts.map((a) => `${formatAmount(a.amount ?? 0, a.currency ?? Currency.Syp)} ${this.currencyCode(a.currency)}`).join(' · ');
  }

  protected hasCompletionRate(offer: BusinessOfferPerformanceDto): boolean {
    return offer.completionRate !== null && offer.completionRate !== undefined;
  }

  protected offerTitle(offer: BusinessOfferPerformanceDto): string {
    return pickLocalized(offer.titleAr, offer.titleEn, this.session.getLanguage());
  }

  protected offerStatusKey(status: SmartOfferStatus | undefined): string {
    switch (status) {
      case SmartOfferStatus.Live:
        return '::Dashboard360:OfferStatus:Live';
      case SmartOfferStatus.BetweenStages:
        return '::Dashboard360:OfferStatus:BetweenStages';
      case SmartOfferStatus.Scheduled:
        return '::Dashboard360:OfferStatus:Scheduled';
      case SmartOfferStatus.Expired:
        return '::Dashboard360:OfferStatus:Expired';
      default:
        return '::Dashboard360:OfferStatus:Paused';
    }
  }

  protected offerStatusClass(status: SmartOfferStatus | undefined): string {
    switch (status) {
      case SmartOfferStatus.Live:
        return 'badge rounded-pill bg-success-subtle text-success-emphasis';
      case SmartOfferStatus.BetweenStages:
      case SmartOfferStatus.Scheduled:
        return 'badge rounded-pill bg-info-subtle text-info-emphasis';
      default:
        return 'badge rounded-pill bg-secondary-subtle text-secondary-emphasis';
    }
  }

  protected insightText(insight: BusinessInsightDto): string {
    const language = this.session.getLanguage();
    switch (insight.kind) {
      case BusinessInsightKind.OfferWithoutOrders:
        return this.localization.instant(
          '::Dashboard360:Insight:OfferWithoutOrders',
          pickLocalized(insight.nameAr, insight.nameEn, language),
        );
      case BusinessInsightKind.LowStockRewards:
        return this.localization.instant('::Dashboard360:Insight:LowStockRewards', formatCount(insight.count ?? 0));
      case BusinessInsightKind.BuyNowExpiringSoon:
        return this.localization.instant('::Dashboard360:Insight:BuyNowExpiringSoon', formatCount(insight.count ?? 0));
      case BusinessInsightKind.UncoveredPeakHour:
        return this.localization.instant('::Dashboard360:Insight:UncoveredPeakHour', `${pad2(insight.hour ?? 0)}:00`);
      case BusinessInsightKind.LowValueCoverage:
        return this.localization.instant('::Dashboard360:Insight:LowValueCoverage', formatPercent(insight.percent));
      default:
        return '';
    }
  }

  protected insightActionKey(insight: BusinessInsightDto): string {
    switch (insight.kind) {
      case BusinessInsightKind.OfferWithoutOrders:
        return '::Dashboard360:Action:OpenDeal';
      case BusinessInsightKind.LowStockRewards:
        return '::Dashboard360:Action:ReviewRewards';
      case BusinessInsightKind.BuyNowExpiringSoon:
        return '::Dashboard360:Action:OpenOrders';
      case BusinessInsightKind.UncoveredPeakHour:
        return '::Dashboard360:Action:AddDeal';
      default:
        return '::Dashboard360:Action:OpenPoints';
    }
  }

  protected insightLink(insight: BusinessInsightDto): string {
    switch (insight.kind) {
      case BusinessInsightKind.OfferWithoutOrders:
      case BusinessInsightKind.UncoveredPeakHour:
        return '/business/smart-offers';
      case BusinessInsightKind.LowStockRewards:
        return '/business/rewards';
      case BusinessInsightKind.BuyNowExpiringSoon:
        return '/business/smart-orders';
      default:
        return '/business/points';
    }
  }

  protected insightIcon(insight: BusinessInsightDto): string {
    switch (insight.kind) {
      case BusinessInsightKind.OfferWithoutOrders:
        return 'fa-tag';
      case BusinessInsightKind.LowStockRewards:
        return 'fa-box-open';
      case BusinessInsightKind.BuyNowExpiringSoon:
        return 'fa-hourglass-half';
      case BusinessInsightKind.UncoveredPeakHour:
        return 'fa-clock';
      default:
        return 'fa-receipt';
    }
  }

  protected isWarning(insight: BusinessInsightDto): boolean {
    return insight.severity === DashboardInsightSeverity.Warning;
  }

  protected customerName(txn: TransactionListItemDto): string {
    const name = [txn.customerFirstName, txn.customerLastName].filter(Boolean).join(' ').trim();
    return name || '—';
  }

  private currencyCode(currency: Currency | undefined): string {
    return this.localization.instant(
      currency === Currency.Usd ? '::Dashboard360:Currency:Usd' : '::Dashboard360:Currency:Syp',
    );
  }

  // The range is sent as explicit dates once the server has told us "today". Before that, the first call leaves both
  // ends unset and the server applies its default window, which is the same 30 days.
  private requestRange(): DashboardRangeDto {
    const today = this.businessToday();
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
      peakHours: this.dashboardService.getPeakHours(range),
      offers: this.dashboardService.getOfferPerformance(range),
      insights: this.dashboardService.getInsights(),
      recent: this.reportsService.getTransactionsList({ skipCount: 0, maxResultCount: 5 }),
    }).subscribe({
      next: (result) => {
        this.summary.set(result.summary);
        this.trends.set(result.trends);
        this.peakHours.set(result.peakHours);
        this.offers.set(result.offers);
        this.insights.set(result.insights);
        this.recentTransactions.set(result.recent.items ?? []);
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
