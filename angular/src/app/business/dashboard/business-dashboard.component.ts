import { DatePipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { ReportsService } from '../../proxy/controllers/reports.service';
import type { DashboardHomeDto, MemberGrowthPointDto, TransactionListItemDto } from '../../proxy/reports/models';
import { PointsTransactionType } from '../../proxy/wallets/points-transaction-type.enum';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { LoadingSpinnerComponent } from '../../shared/components/loading-spinner/loading-spinner.component';
import { ErrorStateComponent } from '../../shared/components/error-state/error-state.component';
import { StatusBadgeComponent, StatusBadgeVariant } from '../../shared/components/status-badge/status-badge.component';

interface GrowthBar {
  label: string;
  value: number;
}

/**
 * Business Portal > Dashboard — mirrors prototype/business/dashboard.html, built entirely from a
 * REAL, already-implemented, purpose-built aggregation endpoint (`IReportsAppService
 * .GetDashboardHomeAsync`, `GET /api/app/report/dashboard-home`) — its own code comment says it
 * "centralizes KPI calculation so the definitions in docs/eksabli-loyalty-platform/features/
 * 07-business-dashboard/README.md#business-rules stay the single source of truth", confirming a
 * Business Dashboard feature was already fully built server-side before this session ever touched
 * Angular. `ReportsController` (whole controller gated on `Eksabli.Reports.Default`) and its Angular
 * proxy (`proxy/reports/*`, `proxy/controllers/reports.service.ts`) both already existed, fully
 * generated — no backend changes needed.
 *
 * Real data, matching the prototype's own four stat tiles exactly:
 * - Active Members / Points Issued / Points Redeemed (all `Last30Days`, real, DB-computed) /
 *   Active Campaigns (`ActiveCampaignCount` — yes, the `Campaign` entity and its own Active/Draft/...
 *   status genuinely exist server-side, even though no Campaigns *page* has been built anywhere in
 *   this app yet — MVP scope exclusions for Admin/Business Portal *pages* don't mean the underlying
 *   feature is fake; this is a real count, just not linked anywhere since there's no page to link to).
 * - No fake "+X%" delta badges under the tiles (`DashboardHomeDto` has no comparison/trend field) —
 *   the one exception is "X% of issued" under Points Redeemed, which IS real: computed client-side
 *   from the same two already-fetched 30-day sums, not fabricated.
 * - Low-stock alert banner: real, from `DashboardHomeDto.LowStockRewards` (`Reward.StockRemaining <=
 *   10`, the same threshold `ReportsAppService` itself uses). Not linked to a "rewards.html"-style
 *   page (matches the prototype) since no Rewards page exists yet in this Business Portal.
 * - Member Growth chart (7 months): real, via `IReportsAppService.GetMemberGrowthAsync` — that
 *   endpoint returns per-day new-member counts for an arbitrary date range, not pre-bucketed by
 *   month, so this component fetches a 7-month window once and re-buckets client-side. The "+X% MoM"
 *   badge is real too, when computable (last two months both present, prior month > 0) — derived from
 *   the same fetched data, not invented; hidden otherwise rather than showing a misleading 0%/∞.
 * - Quick Actions: only links to pages that actually exist in this Business Portal today (Customers,
 *   Employees, Transactions — the last one added once the Transactions live ledger landed) — the
 *   prototype's own "Award points (POS)"/"Create a campaign"/"Add a branch" links are still omitted,
 *   since none of those pages exist yet; a link to a page that doesn't exist is a dead end, not a
 *   shortcut.
 * - Stat tile icons: added to match the prototype's own icon-in-colored-box treatment
 *   (`stat-icon-wrap bg-{color}-50 ... text-{color}-600`), translated to this app's Bootstrap
 *   `-subtle`/`-emphasis` convention (same one `StatusBadgeComponent` uses) instead of the prototype's
 *   raw Tailwind color scale — same icon/color pairing as the prototype (users/primary, plus/success,
 *   gift/warning, bullhorn/info). Purely decorative, `eks-stat-card__icon` is an optional element so
 *   Admin Subscriptions' plain value+label stat tiles (same shared `.eks-stat-card` class) are
 *   unaffected.
 * - Recent Activity table: now real too — the `[MISSING BACKEND CAPABILITY]` gap this comment used to
 *   document is closed. Uses the same `ReportsAppService.GetTransactionsListAsync` endpoint built for
 *   the Transactions page (`ReportsService.getTransactionsList`), unfiltered, `maxResultCount: 5`, so
 *   it's just the 5 most recent rows of the same live ledger, not a separate concept. Customer/Type
 *   badge/points-sign rendering mirrors `business-transactions.component.ts` exactly (same helper
 *   shapes, same localization keys reused from `BusinessPanel:Transactions:*` rather than duplicating
 *   a parallel set under `Dashboard:*`). "View all transactions" links to `/business/transactions`,
 *   matching the prototype's own link.
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
    StatusBadgeComponent,
  ],
})
export class BusinessDashboardComponent implements OnInit {
  private readonly reportsService = inject(ReportsService);

  protected readonly isLoading = signal(true);
  protected readonly loadFailed = signal(false);
  protected readonly home = signal<DashboardHomeDto | null>(null);

  protected readonly redemptionRatePct = computed(() => {
    const home = this.home();
    const issued = home?.pointsIssuedLast30Days ?? 0;
    const redeemed = home?.pointsRedeemedLast30Days ?? 0;
    return issued > 0 ? Math.round((redeemed / issued) * 100) : null;
  });

  protected readonly growthLoading = signal(true);
  protected readonly growthBars = signal<GrowthBar[]>([]);
  protected readonly growthMax = computed(() => Math.max(1, ...this.growthBars().map((b) => b.value)));
  protected readonly growthMomPct = computed(() => {
    const bars = this.growthBars();
    if (bars.length < 2) return null;
    const last = bars[bars.length - 1].value;
    const prior = bars[bars.length - 2].value;
    return prior > 0 ? Math.round(((last - prior) / prior) * 100) : null;
  });

  protected readonly recentActivityLoading = signal(true);
  protected readonly recentActivity = signal<TransactionListItemDto[]>([]);

  ngOnInit(): void {
    this.load();
    this.loadGrowth();
    this.loadRecentActivity();
  }

  protected retry(): void {
    this.load();
  }

  private load(): void {
    this.isLoading.set(true);
    this.loadFailed.set(false);
    this.reportsService.getDashboardHome().subscribe({
      next: (result) => {
        this.home.set(result);
        this.isLoading.set(false);
      },
      error: () => {
        this.isLoading.set(false);
        this.loadFailed.set(true);
      },
    });
  }

  private loadGrowth(): void {
    this.growthLoading.set(true);

    const now = new Date();
    // 7-month window inclusive of the current month, matching the prototype's own "Last 7 months".
    const from = new Date(now.getFullYear(), now.getMonth() - 6, 1);

    this.reportsService.getMemberGrowth({ from: from.toISOString(), to: now.toISOString() }).subscribe({
      next: (points) => {
        this.growthBars.set(this.bucketByMonth(points, from, now));
        this.growthLoading.set(false);
      },
      // Best-effort — the chart card just stays hidden, doesn't block the stat tiles above.
      error: () => this.growthLoading.set(false),
    });
  }

  private loadRecentActivity(): void {
    this.recentActivityLoading.set(true);
    // Unfiltered, newest-first (the repository's own default sort), capped to 5 — the same live ledger
    // the Transactions page browses in full, just its most recent slice.
    this.reportsService.getTransactionsList({ skipCount: 0, maxResultCount: 5 }).subscribe({
      next: (result) => {
        this.recentActivity.set(result.items ?? []);
        this.recentActivityLoading.set(false);
      },
      // Best-effort — same "just stays empty" shape as loadGrowth(), doesn't block the rest of the page.
      error: () => this.recentActivityLoading.set(false),
    });
  }

  protected customerName(txn: TransactionListItemDto): string {
    const name = [txn.customerFirstName, txn.customerLastName].filter(Boolean).join(' ').trim();
    return name || '—';
  }

  protected typeLabelKey(type: PointsTransactionType | undefined): string {
    switch (type) {
      case PointsTransactionType.Redeem:
        return '::BusinessPanel:Transactions:TypeRedeem';
      case PointsTransactionType.Adjust:
        return '::BusinessPanel:Transactions:TypeAdjust';
      case PointsTransactionType.Expire:
        return '::BusinessPanel:Transactions:TypeExpire';
      case PointsTransactionType.Refund:
        return '::BusinessPanel:Transactions:TypeRefund';
      default:
        return '::BusinessPanel:Transactions:TypeEarn';
    }
  }

  protected typeVariant(type: PointsTransactionType | undefined): StatusBadgeVariant {
    switch (type) {
      case PointsTransactionType.Redeem:
        return 'danger';
      case PointsTransactionType.Adjust:
        return 'info';
      case PointsTransactionType.Expire:
        return 'neutral';
      case PointsTransactionType.Refund:
        return 'warning';
      default:
        return 'success';
    }
  }

  /** `GetMemberGrowthAsync` returns one row per DAY with a new-member count — this re-buckets that
   *  into one bar per MONTH across the requested window, filling any month with zero new members
   *  (rather than skipping it, which would misrepresent the trend). */
  private bucketByMonth(points: MemberGrowthPointDto[], from: Date, to: Date): GrowthBar[] {
    const totalsByMonthKey = new Map<string, number>();
    for (const point of points) {
      if (!point.date) continue;
      const date = new Date(point.date);
      const key = `${date.getFullYear()}-${date.getMonth()}`;
      totalsByMonthKey.set(key, (totalsByMonthKey.get(key) ?? 0) + (point.newMembers ?? 0));
    }

    const bars: GrowthBar[] = [];
    const cursor = new Date(from.getFullYear(), from.getMonth(), 1);
    const end = new Date(to.getFullYear(), to.getMonth(), 1);
    while (cursor <= end) {
      const key = `${cursor.getFullYear()}-${cursor.getMonth()}`;
      bars.push({
        label: cursor.toLocaleDateString(undefined, { month: 'short' }),
        value: totalsByMonthKey.get(key) ?? 0,
      });
      cursor.setMonth(cursor.getMonth() + 1);
    }
    return bars;
  }
}
