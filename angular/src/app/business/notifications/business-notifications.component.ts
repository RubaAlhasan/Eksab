import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { LocalizationPipe, PermissionService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { NotificationsService } from '../../proxy/controllers/notifications.service';
import { ReportsService } from '../../proxy/controllers/reports.service';
import { MembershipsService } from '../../proxy/controllers/memberships.service';
import type { NotificationDto, NotificationQuotaUsageDto } from '../../proxy/notifications/models';
import { NotificationChannel } from '../../proxy/notifications/notification-channel.enum';
import { NotificationStatus } from '../../proxy/notifications/notification-status.enum';
import type { NotificationDeliveryRateDto } from '../../proxy/reports/models';
import type { MemberDto } from '../../proxy/memberships/models';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { LoadingSpinnerComponent } from '../../shared/components/loading-spinner/loading-spinner.component';
import { ErrorStateComponent } from '../../shared/components/error-state/error-state.component';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { PaginationComponent } from '../../shared/components/pagination/pagination.component';
import { StatusBadgeComponent, StatusBadgeVariant } from '../../shared/components/status-badge/status-badge.component';
import { ModalComponent } from '../../shared/components/modal/modal.component';

/**
 * Business Portal > Notifications — mirrors prototype/business/notifications.html, built against the
 * real `NotificationAppService` (`SendAsync`/`GetListAsync`/`GetQuotaUsageAsync`, whole
 * `NotificationsController` gated on the single `Eksabli.Notifications.Send` permission — there is no
 * separate "view" permission, confirmed by reading the controller's class-level `[Authorize]`) plus
 * `IReportsAppService.GetNotificationDeliveryRatesAsync` for real delivery stats. No backend changes
 * needed; every endpoint and its proxy already existed.
 *
 * Real fields/behavior, with several deliberate divergences from the prototype's own shape:
 * - **"Compose" sends to exactly ONE membership, not a segment/audience** — `SendNotificationDto`
 *   requires a real `MembershipId` (confirmed by reading it); there is no bulk/broadcast-to-segment
 *   endpoint anywhere in this codebase (that pipeline — `CampaignSegmentEvaluator` + the campaign
 *   sweep worker — is internal to Campaigns, not exposed for ad-hoc sends). The prototype's own
 *   "Audience" dropdown (All members / Gold & Platinum / Inactive 60+ days) is **dropped**,
 *   `[MISSING BACKEND CAPABILITY]`; replaced with a real member search-and-pick (reusing
 *   `MembershipsService.getMembers({ filterText })`, the same real search Coupons/Customers already
 *   use) so Compose sends a real, addressed notification instead of a fake broadcast.
 * - **Channel options match the prototype exactly** — Push/Email/SMS/In-App is the real
 *   `NotificationChannel` enum, not a coincidence.
 * - Title/Message length limits (128 / 1000 chars) are real, straight from `NotificationConsts`.
 * - A sent notification starts life as `Queued` (confirmed by reading `Notification.Create`) and is
 *   dispatched by a background job (`NotificationDispatchJob`) — so the success toast says "queued",
 *   not "sent", matching what actually happens.
 * - **Stat tiles are real but re-derived from what's actually trackable**, not the prototype's fixed
 *   demo numbers:
 *   - "Sent this month" / **"Delivery rate"** (replacing the prototype's fake "Open rate" —
 *     `[MISSING BACKEND CAPABILITY]`, there is no open/click tracking anywhere in this codebase) are
 *     both computed from `GetNotificationDeliveryRatesAsync({ from: startOfMonth, to: now })`,
 *     aggregating across all four channels: `Sent this month = Σsent`, `Delivery rate =
 *     Σsent / (Σsent + Σfailed)` — the exact same "attempts, not queued" formula
 *     `ReportsAppService` itself uses per-channel, just summed.
 *   - **"Sent today / daily limit" replaces the prototype's fake "SMS credits used X/1,000"** — real,
 *     from `GetQuotaUsageAsync` (`NotificationConsts.MaxDailyNotificationsPerTenant` = 500/day,
 *     confirmed by reading it — a genuine per-tenant fan-out cap covering ALL channels together, not a
 *     per-channel SMS-specific credit pool as the prototype implies).
 * - **Delivery Log table**: real, paged, via `GetListAsync` with real `Status`
 *   (Queued/Sent/Failed — three real states, not the prototype's single hardcoded "Sent" badge) and
 *   `Channel` filters. "Campaign" column shows "Linked"/"—" based on the real `CampaignId` presence,
 *   matching the prototype's own binary treatment exactly (no campaign-name lookup — the prototype
 *   itself doesn't show one either). "Sent" date column shows the real `SentAt` (null while `Queued`,
 *   shown as "—" rather than a fabricated date).
 * - No code/title search on the log — `[MISSING BACKEND CAPABILITY]`, `NotificationListFilterDto` has
 *   no `filterText` field, same documented gap as Employees/Support Tickets/Coupons.
 */
type NotificationStatusFilter = '' | NotificationStatus;
type NotificationChannelFilter = '' | NotificationChannel;

@Component({
  selector: 'app-business-notifications',
  templateUrl: './business-notifications.component.html',
  styleUrls: ['./business-notifications.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    ReactiveFormsModule,
    LocalizationPipe,
    PageHeaderComponent,
    LoadingSpinnerComponent,
    ErrorStateComponent,
    EmptyStateComponent,
    PaginationComponent,
    StatusBadgeComponent,
    ModalComponent,
  ],
})
export class BusinessNotificationsComponent implements OnInit {
  private readonly notificationsService = inject(NotificationsService);
  private readonly reportsService = inject(ReportsService);
  private readonly membershipsService = inject(MembershipsService);
  private readonly toaster = inject(ToasterService);
  private readonly permissionService = inject(PermissionService);

  protected readonly Status = NotificationStatus;
  protected readonly Channel = NotificationChannel;
  private readonly pageSize = 10;

  protected readonly canSend = computed(() => this.permissionService.getGrantedPolicy('Eksabli.Notifications.Send'));

  // --- Stat tiles ---
  protected readonly statsLoading = signal(true);
  protected readonly monthSent = signal<number | null>(null);
  protected readonly monthDeliveryRatePct = signal<number | null>(null);
  protected readonly quota = signal<NotificationQuotaUsageDto | null>(null);
  protected readonly quotaPct = computed(() => {
    const quota = this.quota();
    if (!quota?.dailyLimit) return 0;
    return Math.min(100, Math.round(((quota.sentToday ?? 0) / quota.dailyLimit) * 100));
  });

  // --- Delivery log ---
  protected readonly notifications = signal<NotificationDto[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly isLoading = signal(true);
  protected readonly loadFailed = signal(false);
  protected readonly statusFilterValue = signal<NotificationStatusFilter>('');
  protected readonly channelFilterValue = signal<NotificationChannelFilter>('');
  protected readonly pageIndex = signal(0);
  protected readonly totalPages = computed(() => Math.max(1, Math.ceil(this.totalCount() / this.pageSize)));

  // --- Compose modal ---
  protected readonly composeModalOpen = signal(false);
  protected readonly isSending = signal(false);
  protected readonly memberQuery = signal('');
  protected readonly memberResults = signal<MemberDto[]>([]);
  protected readonly isSearchingMembers = signal(false);
  protected readonly selectedMember = signal<MemberDto | null>(null);
  private memberSearchTimer?: ReturnType<typeof setTimeout>;

  protected readonly composeForm = new FormGroup({
    channel: new FormControl(NotificationChannel.Push, { nonNullable: true, validators: [Validators.required] }),
    title: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(128)] }),
    body: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(1000)] }),
  });

  ngOnInit(): void {
    this.load();
    this.loadStats();
    this.loadQuota();
  }

  protected retry(): void {
    this.load();
  }

  protected onStatusFilterChange(event: Event): void {
    const value = (event.target as HTMLSelectElement).value;
    this.statusFilterValue.set(value === '' ? '' : (Number(value) as NotificationStatus));
    this.pageIndex.set(0);
    this.load();
  }

  protected onChannelFilterChange(event: Event): void {
    const value = (event.target as HTMLSelectElement).value;
    this.channelFilterValue.set(value === '' ? '' : (Number(value) as NotificationChannel));
    this.pageIndex.set(0);
    this.load();
  }

  protected goToPage(index: number): void {
    if (index < 0 || index >= this.totalPages()) return;
    this.pageIndex.set(index);
    this.load();
  }

  protected memberName(member: MemberDto): string {
    const name = [member.firstName, member.lastName].filter(Boolean).join(' ').trim();
    return name || member.phoneNumber || '—';
  }

  protected statusLabelKey(status: NotificationStatus | undefined): string {
    switch (status) {
      case NotificationStatus.Sent:
        return '::BusinessPanel:Notifications:StatusSent';
      case NotificationStatus.Failed:
        return '::BusinessPanel:Notifications:StatusFailed';
      default:
        return '::BusinessPanel:Notifications:StatusQueued';
    }
  }

  protected statusVariant(status: NotificationStatus | undefined): StatusBadgeVariant {
    switch (status) {
      case NotificationStatus.Sent:
        return 'success';
      case NotificationStatus.Failed:
        return 'danger';
      default:
        return 'warning';
    }
  }

  protected channelLabelKey(channel: NotificationChannel | undefined): string {
    switch (channel) {
      case NotificationChannel.Email:
        return '::BusinessPanel:Notifications:ChannelEmail';
      case NotificationChannel.Sms:
        return '::BusinessPanel:Notifications:ChannelSms';
      case NotificationChannel.InApp:
        return '::BusinessPanel:Notifications:ChannelInApp';
      default:
        return '::BusinessPanel:Notifications:ChannelPush';
    }
  }

  protected openComposeModal(): void {
    this.selectedMember.set(null);
    this.memberQuery.set('');
    this.memberResults.set([]);
    this.composeForm.reset({ channel: NotificationChannel.Push, title: '', body: '' });
    this.composeModalOpen.set(true);
  }

  protected closeComposeModal(): void {
    this.composeModalOpen.set(false);
  }

  protected onMemberQueryInput(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    this.memberQuery.set(value);
    clearTimeout(this.memberSearchTimer);

    if (value.trim().length < 2) {
      this.memberResults.set([]);
      return;
    }

    this.memberSearchTimer = setTimeout(() => {
      this.isSearchingMembers.set(true);
      this.membershipsService
        .getMembers({ filterText: value, tierId: null, status: null, sorting: undefined, skipCount: 0, maxResultCount: 8 })
        .subscribe({
          next: (result) => {
            this.memberResults.set(result.items ?? []);
            this.isSearchingMembers.set(false);
          },
          error: () => {
            this.isSearchingMembers.set(false);
          },
        });
    }, 300);
  }

  protected selectMember(member: MemberDto): void {
    this.selectedMember.set(member);
    this.memberResults.set([]);
    this.memberQuery.set('');
  }

  protected changeMember(): void {
    this.selectedMember.set(null);
  }

  protected submitCompose(): void {
    const member = this.selectedMember();
    if (!member?.id || this.composeForm.invalid) {
      this.composeForm.markAllAsTouched();
      return;
    }

    const value = this.composeForm.getRawValue();
    this.isSending.set(true);
    this.notificationsService
      .send({ membershipId: member.id, channel: value.channel, title: value.title, body: value.body })
      .subscribe({
        next: () => {
          this.isSending.set(false);
          this.composeModalOpen.set(false);
          this.toaster.success('::BusinessPanel:Notifications:SentMessage');
          this.load();
          this.loadStats();
          this.loadQuota();
        },
        error: () => {
          this.isSending.set(false);
          this.toaster.error('::BusinessPanel:Notifications:SendErrorMessage');
        },
      });
  }

  private load(): void {
    this.isLoading.set(true);
    this.loadFailed.set(false);

    const status = this.statusFilterValue();
    const channel = this.channelFilterValue();

    this.notificationsService
      .getList({
        status: status === '' ? null : status,
        channel: channel === '' ? null : channel,
        campaignId: null,
        sorting: 'creationTime desc',
        skipCount: this.pageIndex() * this.pageSize,
        maxResultCount: this.pageSize,
      })
      .subscribe({
        next: (result) => {
          this.notifications.set(result.items ?? []);
          this.totalCount.set(result.totalCount ?? 0);
          this.isLoading.set(false);
        },
        error: () => {
          this.isLoading.set(false);
          this.loadFailed.set(true);
        },
      });
  }

  private loadStats(): void {
    this.statsLoading.set(true);
    const now = new Date();
    const startOfMonth = new Date(now.getFullYear(), now.getMonth(), 1);

    this.reportsService.getNotificationDeliveryRates({ from: startOfMonth.toISOString(), to: now.toISOString() }).subscribe({
      next: (rates: NotificationDeliveryRateDto[]) => {
        const totalSent = rates.reduce((sum, r) => sum + (r.sent ?? 0), 0);
        const totalFailed = rates.reduce((sum, r) => sum + (r.failed ?? 0), 0);
        const attempts = totalSent + totalFailed;
        this.monthSent.set(totalSent);
        this.monthDeliveryRatePct.set(attempts > 0 ? Math.round((totalSent / attempts) * 100) : null);
        this.statsLoading.set(false);
      },
      // Best-effort — the stat tiles just stay hidden, doesn't block the log below.
      error: () => this.statsLoading.set(false),
    });
  }

  private loadQuota(): void {
    this.notificationsService.getQuotaUsage().subscribe({
      next: (result) => this.quota.set(result),
      error: () => undefined,
    });
  }
}
