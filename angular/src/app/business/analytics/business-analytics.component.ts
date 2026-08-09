import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { forkJoin } from 'rxjs';
import { LocalizationPipe } from '@abp/ng.core';
import { ReportsService } from '../../proxy/controllers/reports.service';
import type { BranchComparisonDto, RedemptionRateReportDto, TierDistributionDto } from '../../proxy/reports/models';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { LoadingSpinnerComponent } from '../../shared/components/loading-spinner/loading-spinner.component';
import { ErrorStateComponent } from '../../shared/components/error-state/error-state.component';

interface Bar {
  label: string;
  value: number;
}

const TIER_COLORS = ['#B45309', '#94A3B8', '#F59E0B', '#6248E3', '#0EA5E9', '#10B981'];

/**
 * Business Portal > Analytics — mirrors prototype/business/analytics.html, built from real endpoints
 * on `IReportsAppService` (whole `ReportsController` gated on `Eksabli.Reports.Default`, same as
 * Dashboard) that Dashboard itself didn't use:
 * - **Member Growth** — same real `GetMemberGrowthAsync` + client-side month-bucketing as
 *   `business-dashboard.component.ts` (duplicated here rather than extracted into a shared service —
 *   matches this codebase's existing convention of each page owning its own small helpers, e.g. every
 *   list page's own `statusLabelKey()` switch).
 * - **Redemption Rate Trend** — real, but NOT a single endpoint call: `GetRedemptionRateAsync` returns
 *   one aggregate rate for whatever `{from, to}` range you give it, no lower-granularity breakdown, so
 *   a genuine 7-point *trend* needs 7 separate real calls (one per month), fired together via
 *   `forkJoin` — a real, different-data-per-call shape, not the "redundant concurrent calls to the
 *   same query" issue documented as gotcha #9's predecessor.
 * - **Redemptions by Branch** — real via `GetBranchComparisonAsync`, but relabeled from the prototype's
 *   own "Active members by branch" — that's not what `BranchComparisonDto.RedemptionCount` actually
 *   measures (it's coupon-redemption counts per branch; `Membership` itself isn't branch-scoped
 *   anywhere in the domain, so "active members by branch" isn't a real, computable thing at all).
 *   Labeled honestly for what the real data is, not what the prototype's copy claimed.
 * - **Tier Distribution** — real via `GetTierDistributionAsync`, a snapshot (no date range).
 * - **KPI Definitions** — only the two that match real, verifiable backend logic: "Active member"
 *   (`ReportsAppService.GetDashboardHomeAsync`'s own code comment: "Membership with >=1 Earn
 *   PointsTransaction in the trailing 30 days") and "Redemption rate" (`RedemptionRateReportDto`'s own
 *   real formula). The prototype's "Churn" and "MRR" definitions are dropped: `GetCustomerSegmentsAsync`
 *   does compute a real Churned segment, but with different real boundaries (no Earn transaction in the
 *   trailing 90 days at all — not the prototype's stated "60+ days") — not shown here since this page
 *   doesn't surface that widget; "MRR" as the prototype describes it (this business's own subscription
 *   cost) isn't exposed by any endpoint this session found.
 *
 * `[MISSING BACKEND CAPABILITY]` / scope reductions, deliberate:
 * - No 7D/30D/90D range toggle — the prototype's own toggle would need to re-drive Redemption Rate
 *   Trend and Redemptions by Branch differently per range while Member Growth/Tier Distribution stay
 *   fixed-shape either way; fixed at a trailing-30-days window for Branch Comparison (matching
 *   Dashboard's own 30-day framing) and a fixed 7-month window for the two trend charts, to keep this
 *   first cut's scope reasonable. A real toggle is a reasonable follow-up, not attempted here.
 * - Customer segment breakdown (`GetCustomerSegmentsAsync`) and Top Customers
 *   (`GetTopCustomersAsync`) are real, unused endpoints not surfaced on this page — this page mirrors
 *   analytics.html specifically; those two map more naturally to reports.html's "Customer Report",
 *   itself not built (see this component's own next-candidate note in NEXT_SESSION_PROMPT.md).
 */
@Component({
  selector: 'app-business-analytics',
  templateUrl: './business-analytics.component.html',
  styleUrls: ['./business-analytics.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [LocalizationPipe, PageHeaderComponent, LoadingSpinnerComponent, ErrorStateComponent],
})
export class BusinessAnalyticsComponent implements OnInit {
  private readonly reportsService = inject(ReportsService);

  protected readonly isLoading = signal(true);
  protected readonly loadFailed = signal(false);

  protected readonly growthBars = signal<Bar[]>([]);
  protected readonly growthMax = computed(() => Math.max(1, ...this.growthBars().map((b) => b.value)));

  protected readonly redemptionBars = signal<Bar[]>([]);
  protected readonly redemptionMax = computed(() => Math.max(1, ...this.redemptionBars().map((b) => b.value)));

  protected readonly branchBars = signal<{ label: string; value: number }[]>([]);
  protected readonly branchMax = computed(() => Math.max(1, ...this.branchBars().map((b) => b.value)));

  protected readonly tierBars = signal<{ label: string; value: number; pct: number; color: string }[]>([]);

  ngOnInit(): void {
    this.load();
  }

  protected retry(): void {
    this.load();
  }

  protected tierColor(index: number): string {
    return TIER_COLORS[index % TIER_COLORS.length];
  }

  private load(): void {
    this.isLoading.set(true);
    this.loadFailed.set(false);

    const now = new Date();
    const monthStarts: Date[] = [];
    for (let i = 6; i >= 0; i--) {
      monthStarts.push(new Date(now.getFullYear(), now.getMonth() - i, 1));
    }
    const windowFrom = monthStarts[0];

    const last30Days = new Date(now.getTime() - 30 * 24 * 60 * 60 * 1000);

    forkJoin({
      growth: this.reportsService.getMemberGrowth({ from: windowFrom.toISOString(), to: now.toISOString() }),
      redemptionRates: forkJoin(
        monthStarts.map((start, i) => {
          const end = i < monthStarts.length - 1 ? monthStarts[i + 1] : now;
          return this.reportsService.getRedemptionRate({ from: start.toISOString(), to: end.toISOString() });
        }),
      ),
      branches: this.reportsService.getBranchComparison({ from: last30Days.toISOString(), to: now.toISOString() }),
      tiers: this.reportsService.getTierDistribution(),
    }).subscribe({
      next: ({ growth, redemptionRates, branches, tiers }) => {
        const totalsByMonthKey = new Map<string, number>();
        for (const point of growth) {
          if (!point.date) continue;
          const date = new Date(point.date);
          const key = `${date.getFullYear()}-${date.getMonth()}`;
          totalsByMonthKey.set(key, (totalsByMonthKey.get(key) ?? 0) + (point.newMembers ?? 0));
        }
        this.growthBars.set(
          monthStarts.map((start) => ({
            label: start.toLocaleDateString(undefined, { month: 'short' }),
            value: totalsByMonthKey.get(`${start.getFullYear()}-${start.getMonth()}`) ?? 0,
          })),
        );

        this.redemptionBars.set(
          monthStarts.map((start, i) => ({
            label: start.toLocaleDateString(undefined, { month: 'short' }),
            value: this.ratePercent(redemptionRates[i]),
          })),
        );

        this.branchBars.set(
          branches
            .filter((b: BranchComparisonDto) => b.branchName)
            .map((b: BranchComparisonDto) => ({ label: b.branchName!, value: b.redemptionCount ?? 0 })),
        );

        this.tierBars.set(this.buildTierBars(tiers));

        this.isLoading.set(false);
      },
      error: () => {
        this.isLoading.set(false);
        this.loadFailed.set(true);
      },
    });
  }

  private ratePercent(dto: RedemptionRateReportDto): number {
    return Math.round((dto.redemptionRate ?? 0) * 100);
  }

  private buildTierBars(tiers: TierDistributionDto[]): { label: string; value: number; pct: number; color: string }[] {
    const total = tiers.reduce((sum, t) => sum + (t.memberCount ?? 0), 0) || 1;
    return tiers.map((t, i) => ({
      label: t.tierName ?? '—',
      value: t.memberCount ?? 0,
      pct: Math.round(((t.memberCount ?? 0) / total) * 100),
      color: this.tierColor(i),
    }));
  }
}
