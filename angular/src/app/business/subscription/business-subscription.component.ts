import { DatePipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { LocalizationPipe, PermissionService } from '@abp/ng.core';
import { ConfirmationService, Confirmation, ToasterService } from '@abp/ng.theme.shared';
import { BillingService } from '../../proxy/controllers/billing.service';
import { SubscriptionPlansService } from '../../proxy/controllers/subscription-plans.service';
import { ReportsService } from '../../proxy/controllers/reports.service';
import { MembershipsService } from '../../proxy/controllers/memberships.service';
import type { SubscriptionPlanDto, TenantSubscriptionDto, UsageDto } from '../../proxy/billing/models';
import { TenantSubscriptionStatus } from '../../proxy/billing/tenant-subscription-status.enum';
import { MembershipStatus } from '../../proxy/memberships/membership-status.enum';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { LoadingSpinnerComponent } from '../../shared/components/loading-spinner/loading-spinner.component';
import { ErrorStateComponent } from '../../shared/components/error-state/error-state.component';
import { StatusBadgeComponent, StatusBadgeVariant } from '../../shared/components/status-badge/status-badge.component';

/** Same well-known `FeatureLimitsJson` keys as `admin-plans.component.ts` (per the doc comment on
 *  `SubscriptionPlan.cs`) — read-only subset needed here, no form/serialization side since this page
 *  never edits a plan's limits, only reads the CURRENT plan's for the usage widget. Not shared as a
 *  cross-page util (same "small enough per-page duplication is fine" precedent as the bulk-lookup Map
 *  pattern elsewhere in this app). */
const FEATURE_KEYS = {
  maxActiveMembers: 'Eksabli.MaxActiveMembers',
  maxCampaigns: 'Eksabli.MaxCampaigns',
  smsNotifications: 'Eksabli.SMSNotifications',
  pushNotifications: 'Eksabli.PushNotifications',
} as const;

// FeatureLimitsJson values are strings on the backend (Dictionary<string,string> — see the doc comment
// on SubscriptionPlan.cs), e.g. "5"/"true", but tolerate raw numbers/booleans too for plans saved
// before admin-plans.component.ts was fixed to always write strings.
function parseFeatureLimit(json: string | undefined, key: string): number | null {
  try {
    const raw = json ? (JSON.parse(json) as Record<string, unknown>) : {};
    const value = raw[key];
    if (typeof value === 'number') return value;
    if (typeof value === 'string' && value.trim() !== '') {
      const parsed = Number(value);
      return Number.isFinite(parsed) ? parsed : null;
    }
    return null;
  } catch {
    return null;
  }
}

function parseFeatureToggle(json: string | undefined, key: string): boolean {
  try {
    const raw = json ? (JSON.parse(json) as Record<string, unknown>) : {};
    const value = raw[key];
    if (typeof value === 'boolean') return value;
    if (typeof value === 'string') return value === 'true';
    return false;
  } catch {
    return false;
  }
}

/**
 * Business Portal > Subscription — mirrors prototype/business/subscription.html, built against the
 * real `IBillingAppService` (`GetMyCurrentSubscriptionAsync`/`GetMyUsageAsync`/`ChangePlanAsync`)
 * plus `ISubscriptionPlanAppService.GetListAsync` (the same public pricing catalog Admin Plans/the
 * registration flow already use) and two already-fetched real counts (`IReportsAppService
 * .GetDashboardHomeAsync().ActiveCampaignCount`, `MembershipsService.getMembers` with a status
 * filter). No backend changes needed; every endpoint and its proxy already existed.
 *
 * Real fields, with several deliberate divergences from the prototype's own shape:
 * - **Plan name/status/renewal date are real** (`GetMyCurrentSubscriptionAsync`). Status shows all 4
 *   real `TenantSubscriptionStatus` values (Trialing/Active/PastDue/Cancelled), not the prototype's
 *   single hardcoded green badge. Monthly price isn't on `TenantSubscriptionDto` itself — fetched via
 *   `SubscriptionPlansService.get(planId)` (a second real call, not fabricated).
 * - **Usage widget is real but honestly re-scoped** — `UsageDto` only carries Branches (confirmed by
 *   reading it), so the other rows are assembled from what's actually measurable elsewhere:
 *   - **Branches**: real used/limit, same source `business-branches.component.ts`'s own quota banner
 *     uses. This is the ONE feature actually enforced with a real `UserFriendlyException` at the limit
 *     (`BranchAppService.CreateAsync`).
 *   - **Campaigns**: real used, from `DashboardHomeDto.ActiveCampaignCount` — confirmed (by reading
 *     `ReportsAppService.GetDashboardHomeAsync`) to count `CampaignStatus.Active` campaigns, the EXACT
 *     same count `CampaignAppService.CreateAsync`'s own quota check uses — a genuinely matching
 *     "used" figure, not a proxy metric. Real limit parsed from the current plan's `FeatureLimitsJson`
 *     (`Eksabli.MaxCampaigns`). This one IS enforced too.
 *   - **Active Members**: real used, via `MembershipsService.getMembers({ status: Active,
 *     maxResultCount: 1 })`'s `totalCount` (same `maxResultCount: 1` count-only trick documented in
 *     this app's own gotcha #9 — NOT `0`, which 400s). Real limit from `FeatureLimitsJson`
 *     (`Eksabli.MaxActiveMembers`) — but confirmed (by grepping the whole Application layer) this
 *     limit is NOT enforced anywhere; nothing blocks a business from exceeding it today. Shown as
 *     informational usage, not a hard cap, and the template says so.
 *   - **SMS / Push Notifications**: real per-plan toggles from `FeatureLimitsJson`
 *     (`Eksabli.SMSNotifications`/`Eksabli.PushNotifications`), shown as Enabled/Disabled — not a
 *     "credits" bar. `[MISSING BACKEND CAPABILITY]`: the prototype's own "SMS Credits used 640/1,000"
 *     row has no real backend behind it (no SMS-send-count-vs-quota concept exists; the closest real
 *     analog, `NotificationConsts.MaxDailyNotificationsPerTenant`, is a flat 500/day cap shared across
 *     ALL channels together, already surfaced on the Notifications page — a different concept, not
 *     reused here to avoid implying a fake per-channel credit pool).
 * - **Compare Plans is real** (`SubscriptionPlansService.getList()`, the same public catalog Admin
 *   Plans manages), current plan highlighted.
 * - **"Select" on another plan performs a REAL, immediate `ChangePlanAsync` call** — a deliberate
 *   divergence from the prototype, whose own Upgrade/Downgrade/Select buttons are all stubs
 *   ("this would open the plan checkout flow"). The real backend has no payment/proration step at all
 *   (confirmed by reading `BillingAppService.ChangePlanAsync` — it just reassigns `PlanId` and re-pushes
 *   `FeatureLimitsJson` into Feature Management), so faking a checkout step here would be LESS honest
 *   than just performing the real, complete action. Guarded behind a confirmation dialog since it's a
 *   real mutation with billing-sounding consequences.
 * - **No Cancel / Danger Zone** — `[MISSING BACKEND CAPABILITY]`. `TenantSubscription.Cancel()` exists
 *   as a real domain method (confirmed by reading `TenantSubscription.cs`) but is not called from ANY
 *   app service anywhere in this codebase — not `IBillingAppService`, not even the Host-only
 *   `IAdminSubscriptionAppService` (confirmed by reading both interfaces). It is genuinely unreachable
 *   via any API today; not built rather than wiring a button to a dead end.
 */
@Component({
  selector: 'app-business-subscription',
  templateUrl: './business-subscription.component.html',
  styleUrls: ['./business-subscription.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, DecimalPipe, LocalizationPipe, PageHeaderComponent, LoadingSpinnerComponent, ErrorStateComponent, StatusBadgeComponent],
})
export class BusinessSubscriptionComponent implements OnInit {
  private readonly billingService = inject(BillingService);
  private readonly plansService = inject(SubscriptionPlansService);
  private readonly reportsService = inject(ReportsService);
  private readonly membershipsService = inject(MembershipsService);
  private readonly confirmation = inject(ConfirmationService);
  private readonly toaster = inject(ToasterService);
  private readonly permissionService = inject(PermissionService);

  protected readonly Status = TenantSubscriptionStatus;

  protected readonly canManage = computed(() => this.permissionService.getGrantedPolicy('Eksabli.Billing.ManageOwn'));

  protected readonly isLoading = signal(true);
  protected readonly loadFailed = signal(false);
  protected readonly subscription = signal<TenantSubscriptionDto | null>(null);
  protected readonly currentPlan = signal<SubscriptionPlanDto | null>(null);
  protected readonly usage = signal<UsageDto | null>(null);
  protected readonly activeCampaignCount = signal<number | null>(null);
  protected readonly activeMemberCount = signal<number | null>(null);
  protected readonly plans = signal<SubscriptionPlanDto[]>([]);
  protected readonly changingPlanId = signal<string | null>(null);

  protected readonly maxCampaigns = computed(() => parseFeatureLimit(this.currentPlan()?.featureLimitsJson, FEATURE_KEYS.maxCampaigns));
  protected readonly maxActiveMembers = computed(() =>
    parseFeatureLimit(this.currentPlan()?.featureLimitsJson, FEATURE_KEYS.maxActiveMembers),
  );
  protected readonly smsEnabled = computed(() => parseFeatureToggle(this.currentPlan()?.featureLimitsJson, FEATURE_KEYS.smsNotifications));
  protected readonly pushEnabled = computed(() => parseFeatureToggle(this.currentPlan()?.featureLimitsJson, FEATURE_KEYS.pushNotifications));

  ngOnInit(): void {
    this.load();
  }

  protected retry(): void {
    this.load();
  }

  protected usagePct(used: number | null, max: number | null): number {
    if (used == null || !max) return 0;
    return Math.min(100, Math.round((used / max) * 100));
  }

  protected isOverLimit(used: number | null, max: number | null): boolean {
    return used != null && max != null && used > max;
  }

  protected statusLabelKey(status: TenantSubscriptionStatus | undefined): string {
    switch (status) {
      case TenantSubscriptionStatus.Active:
        return '::BusinessPanel:Subscription:StatusActive';
      case TenantSubscriptionStatus.PastDue:
        return '::BusinessPanel:Subscription:StatusPastDue';
      case TenantSubscriptionStatus.Cancelled:
        return '::BusinessPanel:Subscription:StatusCancelled';
      default:
        return '::BusinessPanel:Subscription:StatusTrialing';
    }
  }

  protected statusVariant(status: TenantSubscriptionStatus | undefined): StatusBadgeVariant {
    switch (status) {
      case TenantSubscriptionStatus.Active:
        return 'success';
      case TenantSubscriptionStatus.PastDue:
        return 'warning';
      case TenantSubscriptionStatus.Cancelled:
        return 'danger';
      default:
        return 'info';
    }
  }

  protected selectPlan(plan: SubscriptionPlanDto): void {
    if (!plan.id || plan.id === this.currentPlan()?.id) return;

    this.confirmation
      .warn('::BusinessPanel:Subscription:ChangePlanConfirmMessage', '::BusinessPanel:Subscription:ChangePlanConfirmTitle', {
        yesText: '::BusinessPanel:Subscription:ChangePlanConfirmYes',
      })
      .subscribe((status) => {
        if (status !== Confirmation.Status.confirm || !plan.id) return;

        this.changingPlanId.set(plan.id);
        this.billingService.changePlan({ planId: plan.id }).subscribe({
          next: () => {
            this.changingPlanId.set(null);
            this.toaster.success('::BusinessPanel:Subscription:PlanChangedMessage');
            this.load();
          },
          error: () => {
            this.changingPlanId.set(null);
            this.toaster.error('::BusinessPanel:Subscription:PlanChangeErrorMessage');
          },
        });
      });
  }

  private load(): void {
    this.isLoading.set(true);
    this.loadFailed.set(false);

    this.billingService.getMyCurrentSubscription().subscribe({
      next: (subscription) => {
        this.subscription.set(subscription);
        this.isLoading.set(false);
        if (subscription.planId) this.loadCurrentPlan(subscription.planId);
      },
      error: () => {
        this.isLoading.set(false);
        this.loadFailed.set(true);
      },
    });

    this.billingService.getMyUsage().subscribe({
      next: (result) => this.usage.set(result),
      error: () => undefined,
    });

    this.plansService.getList({ sorting: 'monthlyPrice asc', skipCount: 0, maxResultCount: 20 }).subscribe({
      next: (result) => this.plans.set(result.items ?? []),
      error: () => undefined,
    });

    this.reportsService.getDashboardHome().subscribe({
      next: (result) => this.activeCampaignCount.set(result.activeCampaignCount ?? 0),
      error: () => undefined,
    });

    this.membershipsService
      .getMembers({ filterText: null, tierId: null, status: MembershipStatus.Active, sorting: undefined, skipCount: 0, maxResultCount: 1 })
      .subscribe({
        next: (result) => this.activeMemberCount.set(result.totalCount ?? 0),
        error: () => undefined,
      });
  }

  private loadCurrentPlan(planId: string): void {
    this.plansService.get(planId).subscribe({
      next: (plan) => this.currentPlan.set(plan),
      error: () => undefined,
    });
  }
}
