import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { LocalizationPipe, PermissionService } from '@abp/ng.core';
import { AdminPlatformReportsService } from '../../proxy/controllers/admin-platform-reports.service';
import { AdminSubscriptionsService } from '../../proxy/controllers/admin-subscriptions.service';
import { AdminTenantsService } from '../../proxy/controllers/admin-tenants.service';
import { CategoriesService } from '../../proxy/controllers/categories.service';
import type { SupportTicketMetricsDto, TenantGrowthPointDto } from '../../proxy/platform-reports/models';
import type { CategoryDto } from '../../proxy/platform/models';
import { SupportTicketPriority } from '../../proxy/platform/support-ticket-priority.enum';
import { SupportTicketStatus } from '../../proxy/platform/support-ticket-status.enum';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { LoadingSpinnerComponent } from '../../shared/components/loading-spinner/loading-spinner.component';
import { ErrorStateComponent } from '../../shared/components/error-state/error-state.component';

/**
 * Admin Portal > Reports — the "cheap" platform-wide report subset: tenant growth and support-ticket
 * volume are genuinely new (`AdminPlatformReportAppService`, Host-scoped). Category mix and MRR are
 * NOT recomputed here — they already exist and are already shown on the Dashboard
 * (`CategoriesService.getList` → `CategoryDto.businessCount`, `AdminSubscriptionsService.getStats`/
 * `.getMrrTrend`) — this page calls those same real endpoints directly rather than shipping a second,
 * potentially-divergent copy of the same numbers.
 *
 * Deliberately NOT built: DAU/MAU (no session/activity-tracking infrastructure exists anywhere in this
 * codebase — a much larger initiative, not a report), and support-ticket resolution-time (`SupportTicket`
 * has no `ResolvedAt`/`ClosedAt` field; `LastModificationTime` isn't a safe proxy since `AddMessage`
 * bumps it on every reply, not just on resolution). Volume-only ships instead of a fabricated metric.
 */
@Component({
  selector: 'app-admin-reports',
  templateUrl: './admin-reports.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DecimalPipe, LocalizationPipe, PageHeaderComponent, LoadingSpinnerComponent, ErrorStateComponent],
})
export class AdminReportsComponent implements OnInit {
  private readonly reportsService = inject(AdminPlatformReportsService);
  private readonly subscriptionsService = inject(AdminSubscriptionsService);
  private readonly tenantsService = inject(AdminTenantsService);
  private readonly categoriesService = inject(CategoriesService);
  private readonly permissionService = inject(PermissionService);

  protected readonly TicketStatus = SupportTicketStatus;
  protected readonly TicketPriority = SupportTicketPriority;

  protected readonly canViewMrr = computed(() => this.permissionService.getGrantedPolicy('Eksabli.Billing.ManagePlatform'));

  protected readonly businessesLoading = signal(true);
  protected readonly totalBusinesses = signal<number | null>(null);

  protected readonly mrrLoading = signal(false);
  protected readonly mrr = signal<number | null>(null);

  protected readonly growthLoading = signal(true);
  protected readonly growthFailed = signal(false);
  private readonly growthPoints = signal<TenantGrowthPointDto[]>([]);
  protected readonly growthBars = computed(() =>
    this.growthPoints().map((p) => ({
      label: new Date(p.year, p.month - 1, 1).toLocaleDateString(undefined, { month: 'short' }),
      value: p.newTenants,
    })),
  );
  protected readonly growthMax = computed(() => Math.max(1, ...this.growthBars().map((b) => b.value)));

  protected readonly ticketsLoading = signal(true);
  protected readonly ticketsFailed = signal(false);
  protected readonly ticketMetrics = signal<SupportTicketMetricsDto | null>(null);
  protected readonly statusRows = computed(() => {
    const metrics = this.ticketMetrics();
    if (!metrics) return [];
    const max = Math.max(1, ...Object.values(metrics.countByStatus));
    return [SupportTicketStatus.Open, SupportTicketStatus.InProgress, SupportTicketStatus.Resolved, SupportTicketStatus.Closed].map(
      (status) => ({ status, count: metrics.countByStatus[status] ?? 0, pct: ((metrics.countByStatus[status] ?? 0) / max) * 100 }),
    );
  });
  protected readonly priorityRows = computed(() => {
    const metrics = this.ticketMetrics();
    if (!metrics) return [];
    const max = Math.max(1, ...Object.values(metrics.countByPriority));
    return [SupportTicketPriority.Low, SupportTicketPriority.Medium, SupportTicketPriority.High, SupportTicketPriority.Urgent].map(
      (priority) => ({ priority, count: metrics.countByPriority[priority] ?? 0, pct: ((metrics.countByPriority[priority] ?? 0) / max) * 100 }),
    );
  });

  protected readonly categoriesLoading = signal(true);
  private readonly categories = signal<CategoryDto[]>([]);
  protected readonly topCategories = computed(() =>
    [...this.categories()].sort((a, b) => b.businessCount - a.businessCount).slice(0, 5),
  );
  protected readonly categoryMixMax = computed(() => Math.max(1, ...this.topCategories().map((c) => c.businessCount)));

  ngOnInit(): void {
    this.loadBusinessCount();
    this.loadGrowth();
    this.loadTickets();
    this.loadCategories();
    if (this.canViewMrr()) this.loadMrr();
  }

  protected ticketStatusLabelKey(status: SupportTicketStatus): string {
    switch (status) {
      case SupportTicketStatus.Open:
        return '::AdminPanel:SupportTickets:StatusOpen';
      case SupportTicketStatus.InProgress:
        return '::AdminPanel:SupportTickets:StatusInProgress';
      case SupportTicketStatus.Resolved:
        return '::AdminPanel:SupportTickets:StatusResolved';
      default:
        return '::AdminPanel:SupportTickets:StatusClosed';
    }
  }

  protected ticketPriorityLabelKey(priority: SupportTicketPriority): string {
    switch (priority) {
      case SupportTicketPriority.Urgent:
        return '::AdminPanel:SupportTickets:PriorityUrgent';
      case SupportTicketPriority.High:
        return '::AdminPanel:SupportTickets:PriorityHigh';
      case SupportTicketPriority.Medium:
        return '::AdminPanel:SupportTickets:PriorityMedium';
      default:
        return '::AdminPanel:SupportTickets:PriorityLow';
    }
  }

  protected retryGrowth(): void {
    this.loadGrowth();
  }

  protected retryTickets(): void {
    this.loadTickets();
  }

  private loadBusinessCount(): void {
    this.businessesLoading.set(true);
    // Only the total count is needed here — `maxResultCount: 1` (the lowest value ABP's own
    // LimitedResultRequestDto accepts, `0` gets a 400) reads `totalCount` without transferring a full
    // list, same pattern admin-dashboard.component.ts uses for its Open Tickets count.
    this.tenantsService.getList({ filterText: null, approvalStatus: null, skipCount: 0, maxResultCount: 1 }).subscribe({
      next: (result) => {
        this.totalBusinesses.set(result.totalCount ?? 0);
        this.businessesLoading.set(false);
      },
      error: () => this.businessesLoading.set(false),
    });
  }

  private loadGrowth(): void {
    this.growthLoading.set(true);
    this.growthFailed.set(false);
    this.reportsService.getTenantGrowth().subscribe({
      next: (points) => {
        this.growthPoints.set(points);
        this.growthLoading.set(false);
      },
      error: () => {
        this.growthLoading.set(false);
        this.growthFailed.set(true);
      },
    });
  }

  private loadTickets(): void {
    this.ticketsLoading.set(true);
    this.ticketsFailed.set(false);
    this.reportsService.getTicketMetrics().subscribe({
      next: (metrics) => {
        this.ticketMetrics.set(metrics);
        this.ticketsLoading.set(false);
      },
      error: () => {
        this.ticketsLoading.set(false);
        this.ticketsFailed.set(true);
      },
    });
  }

  private loadMrr(): void {
    this.mrrLoading.set(true);
    this.subscriptionsService.getStats().subscribe({
      next: (stats) => {
        this.mrr.set(stats.approxMrr);
        this.mrrLoading.set(false);
      },
      error: () => this.mrrLoading.set(false),
    });
  }

  private loadCategories(): void {
    this.categoriesLoading.set(true);
    this.categoriesService.getList({ parentCategoryId: null, filterText: null, skipCount: 0, maxResultCount: 500 }).subscribe({
      next: (result) => {
        this.categories.set(result.items ?? []);
        this.categoriesLoading.set(false);
      },
      error: () => this.categoriesLoading.set(false),
    });
  }
}
