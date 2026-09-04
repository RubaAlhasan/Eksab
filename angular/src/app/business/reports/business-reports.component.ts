import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { LocalizationPipe } from '@abp/ng.core';
import { ReportsService } from '../../proxy/controllers/reports.service';
import type { CustomerSegmentReportDto, TopCustomerDto } from '../../proxy/reports/models';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { LoadingSpinnerComponent } from '../../shared/components/loading-spinner/loading-spinner.component';
import { ErrorStateComponent } from '../../shared/components/error-state/error-state.component';

interface SegmentBar {
  key: 'new' | 'active' | 'atRisk' | 'churned';
  labelKey: string;
  definitionKey: string;
  value: number;
  pct: number;
  color: string;
}

// These four are lifecycle STATES, not chart series, so they take the reserved status tokens
// (info / success / warning) plus a muted neutral for "churned" — never the `--eks-chart-*`
// categorical slots, which are for identity. Each bar is labelled in the template, so state is
// never carried by color alone.
const SEGMENT_COLORS: Record<SegmentBar['key'], string> = {
  new: 'var(--eks-info)',
  active: 'var(--eks-success)',
  atRisk: 'var(--eks-warning)',
  churned: 'var(--eks-text-faint)',
};

/**
 * Business Portal > Reports — the route/nav slot `prototype/business/reports.html` occupies, but
 * NOT a faithful rebuild of that mockup. A prior session already evaluated it (see
 * `NEXT_SESSION_PROMPT.md`'s "reports.html was checked and found to be a weak next-candidate") and
 * found every one of its four "Generate as CSV/PDF" report cards + its "Recent Exports" history table
 * has no real backend behind it: `IReportsAppService`'s only true file-generation capability is the
 * single token-gated Excel export (`GetTransactionsAsExcelFileAsync`, already built — see Transactions'
 * own page), no CSV/PDF anywhere, and no `Export`/`GeneratedReport` entity exists to back a history
 * list — every export in this app is generated fresh, nothing to persist or list. Re-reading the real
 * `IReportsAppService` surface this session confirmed that finding still holds, AND that three of the
 * prototype's four report categories already have a real, better home elsewhere in this app:
 * - "Business Report" (member growth, redemption rate, branch comparison) = `/business/analytics`,
 *   verbatim.
 * - "Financial Report" (subscription cost, invoices, quota usage) = `/business/billing` +
 *   `/business/subscription`. `IReportsAppService` doesn't expose subscription/billing data at all —
 *   it's a different app service entirely (`ISubscriptionAppService`/`IBillingAppService`).
 * - "Marketing Report" (campaign performance, notification delivery rates) = `/business/campaigns`
 *   (per-campaign Sent/Rewarded/Bonus Points, this session's own work) + `/business/notifications`
 *   (delivery rate, already built).
 * - "Customer Report" (segment breakdown, top customers by lifetime value) is the one piece that was
 *   genuinely NOT built anywhere: `GetCustomerSegmentsAsync`/`GetTopCustomersAsync` are real, fully
 *   working endpoints with zero UI surface until this page. That's this page's actual, honest content.
 *
 * So instead of four fake "Generate" buttons, this page is: a real Customer Report inline (the one new
 * thing), plus three honest "go to the real page" cards for the other three categories, plus a link to
 * the one real export that exists (Transactions). No backend changes needed — every endpoint here
 * already existed, including the two that had never been surfaced before.
 */
@Component({
  selector: 'app-business-reports',
  templateUrl: './business-reports.component.html',
  styleUrls: ['./business-reports.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, LocalizationPipe, PageHeaderComponent, LoadingSpinnerComponent, ErrorStateComponent],
})
export class BusinessReportsComponent implements OnInit {
  private readonly reportsService = inject(ReportsService);

  protected readonly isLoading = signal(true);
  protected readonly loadFailed = signal(false);

  private readonly segments = signal<CustomerSegmentReportDto | null>(null);
  protected readonly topCustomers = signal<TopCustomerDto[]>([]);

  protected readonly segmentBars = computed<SegmentBar[]>(() => {
    const s = this.segments();
    if (!s) return [];
    const counts: Record<SegmentBar['key'], number> = {
      new: s.new ?? 0,
      active: s.active ?? 0,
      atRisk: s.atRisk ?? 0,
      churned: s.churned ?? 0,
    };
    const total = counts.new + counts.active + counts.atRisk + counts.churned || 1;
    const keys: SegmentBar['key'][] = ['new', 'active', 'atRisk', 'churned'];
    return keys.map((key) => ({
      key,
      labelKey: `::BusinessPanel:Reports:Segment${key === 'atRisk' ? 'AtRisk' : key.charAt(0).toUpperCase() + key.slice(1)}`,
      definitionKey: `::BusinessPanel:Reports:Segment${key === 'atRisk' ? 'AtRisk' : key.charAt(0).toUpperCase() + key.slice(1)}Definition`,
      value: counts[key],
      pct: Math.round((counts[key] / total) * 100),
      color: SEGMENT_COLORS[key],
    }));
  });

  protected readonly segmentTotal = computed(() => this.segmentBars().reduce((sum, bar) => sum + bar.value, 0));

  ngOnInit(): void {
    this.load();
  }

  protected retry(): void {
    this.load();
  }

  protected customerName(customer: TopCustomerDto): string | null {
    const name = [customer.firstName, customer.lastName].filter(Boolean).join(' ').trim();
    return name || null;
  }

  protected customerInitials(customer: TopCustomerDto): string {
    const name = this.customerName(customer);
    if (!name) return '?';
    return name
      .split(' ')
      .filter(Boolean)
      .map((word) => word[0])
      .join('')
      .slice(0, 2)
      .toUpperCase();
  }

  private load(): void {
    this.isLoading.set(true);
    this.loadFailed.set(false);

    forkJoin({
      segments: this.reportsService.getCustomerSegments(),
      topCustomers: this.reportsService.getTopCustomers(10),
    }).subscribe({
      next: ({ segments, topCustomers }) => {
        this.segments.set(segments);
        this.topCustomers.set(topCustomers ?? []);
        this.isLoading.set(false);
      },
      error: () => {
        this.isLoading.set(false);
        this.loadFailed.set(true);
      },
    });
  }
}
