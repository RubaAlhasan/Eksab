import { DatePipe, DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { LocalizationPipe, PermissionService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { MembershipsService } from '../../proxy/controllers/memberships.service';
import { FollowsService } from '../../proxy/controllers/follows.service';
import { TiersService } from '../../proxy/controllers/tiers.service';
import { PosService } from '../../proxy/controllers/pos.service';
import type { MemberDto } from '../../proxy/memberships/models';
import type { FollowerDto } from '../../proxy/engagement/models';
import type { TierDto } from '../../proxy/wallets/models';
import { MembershipStatus } from '../../proxy/memberships/membership-status.enum';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { SearchInputComponent } from '../../shared/components/search-input/search-input.component';
import { StatusBadgeComponent, StatusBadgeVariant } from '../../shared/components/status-badge/status-badge.component';
import { LoadingSpinnerComponent } from '../../shared/components/loading-spinner/loading-spinner.component';
import { ErrorStateComponent } from '../../shared/components/error-state/error-state.component';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { PaginationComponent } from '../../shared/components/pagination/pagination.component';
import { ModalComponent } from '../../shared/components/modal/modal.component';

type CustomersTab = 'members' | 'following';

/**
 * Business Portal > Customers — mirrors prototype/business/customers.html's shape, but only where the
 * data is genuinely real:
 * - Members tab: `MembershipsService.getMembers` — a NEW endpoint added earlier this session
 *   (`MembershipAppService.GetMembersAsync`, `Eksabli.Memberships.View`) joining this tenant's
 *   `Membership` with Host-realm `CustomerProfile`/`IdentityUser` (name/phone) and this tenant's
 *   `PointsWallet`/`Tier` (balance/tier) — same cross-realm join shape `PosAppService
 *   .LookupCustomerByPhoneAsync` already used for a single customer, just batched. Real fields only:
 *   name, phone, tier, balance, joined date, status. "Last Active" is `PointsWallet
 *   .LastModificationTime` (bumped by ABP's own auditing whenever a real points transaction changes
 *   the wallet) — the closest genuine signal, since there's no login/session tracking anywhere in
 *   this codebase.
 * - Following tab: `FollowsService.getFollowers` — real, `Eksabli.Followers.View`, also enriched with
 *   name/phone earlier this session (previously bare `FollowDto`, no name to show).
 *
 * Routing history: this page briefly lived at `/admin/customers` (folded into the Admin Portal shell)
 * after the user said "must be under admin" — then moved back here, to its own `/business` shell, after
 * the user's later, equally explicit "must be under business/employees" + "when login business show
 * page related to business like prototype". Both instructions were real and honored when given; this is
 * genuinely where it ended up. `businessRealmGuard` (core/guards/business.guard.ts) now gates the whole
 * `/business` shell via real tenant-resolution (`currentTenant.id` from ABP's own config state), not a
 * permission heuristic — see that file's comment for why this is reliable without any custom
 * subdomain/header tenant-selection UI.
 *
 * `[MISSING BACKEND CAPABILITY]`, deliberately not built:
 * - Export button — no Excel-export capability exists for Memberships (unlike Books/Authors' real
 *   token-gated pattern), so there's nothing to wire it to.
 * - "Convert to Campaign Target" per follower — `Eksabli.Followers.ConvertToCampaign` permission
 *   exists but is explicitly "defined for parity/future use" (see EksabliPermissions.cs), no endpoint
 *   backs it. Not rendered — a button with nothing behind it is a dead end, not a feature.
 * - A separate customer-details DRILL-DOWN PAGE (`prototype/business/customer-details.html`) was
 *   checked and found not real-buildable as its own `/business/customers/:id` route: there is no
 *   single-member `GetAsync(id)` (only the paged `GetMembersAsync`), no staff-facing per-member
 *   transaction history (`IWalletAppService.GetMyTransactionHistoryAsync` is self-service-only), and
 *   `CouponAuditFilterDto` has no `MembershipId` filter to scope a "Coupons Redeemed" tab. Customer
 *   names stay plain text, not links to a page that doesn't exist.
 * - Real `MembershipStatus` is only Active/Frozen (confirmed in the domain enum) — the prototype's
 *   "At Risk"/"Churned" statuses don't exist anywhere in the backend and are not reproduced; the
 *   status filter only offers the two real values.
 *
 * **One real piece of `customer-details.html` WAS pulled forward into this page instead**: its
 * "Manual Point Adjustment" modal is real (`PosAppService.ManualAdjustAsync`, same
 * no-ABP-permission/custom-staff-role-check shape as `business-points.component.ts`'s Award tab —
 * Owner/BranchManager only, confirmed by reading `CheckStaffRoleAsync`'s call site there) — added here
 * as a real per-row action instead of a drill-down page, since every field it needs (`MemberDto
 * .customerId`, already loaded) is already on hand. **The prototype's own "Daily adjustment cap: 200
 * pts per staff member" copy is WRONG relative to the real backend** — confirmed by reading
 * `PosAppService.ManualAdjustAsync`/`PointsTransactionConsts`: the real cap is 20 ADJUSTMENTS per
 * employee per day (a count, not a cumulative points ceiling), so this page does not reproduce the
 * prototype's "200 pts" copy or attempt a client-side points cap — the real count cap has no live
 * "X used today" query to show proactively, so it just surfaces as the real `UserFriendlyException`
 * toast if hit, same pattern as every other plan/quota limit in this app.
 */
@Component({
  selector: 'app-business-customers',
  templateUrl: './business-customers.component.html',
  styleUrls: ['./business-customers.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    DecimalPipe,
    ReactiveFormsModule,
    LocalizationPipe,
    PageHeaderComponent,
    SearchInputComponent,
    StatusBadgeComponent,
    LoadingSpinnerComponent,
    ErrorStateComponent,
    EmptyStateComponent,
    PaginationComponent,
    ModalComponent,
  ],
})
export class BusinessCustomersComponent implements OnInit {
  private readonly membershipsService = inject(MembershipsService);
  private readonly followsService = inject(FollowsService);
  private readonly tiersService = inject(TiersService);
  private readonly posService = inject(PosService);
  private readonly toaster = inject(ToasterService);
  private readonly permissionService = inject(PermissionService);

  protected readonly Status = MembershipStatus;
  private readonly pageSize = 10;

  protected readonly activeTab = signal<CustomersTab>('members');

  protected readonly members = signal<MemberDto[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly isLoading = signal(true);
  protected readonly loadFailed = signal(false);
  protected readonly filterText = signal('');
  protected readonly tierFilterValue = signal('');
  protected readonly statusFilterValue = signal('');
  protected readonly pageIndex = signal(0);
  protected readonly totalPages = computed(() => Math.max(1, Math.ceil(this.totalCount() / this.pageSize)));

  /** Best-effort — `TiersController` is gated on `Eksabli.Tiers.Default` for its whole controller
   *  (including the read), a permission a Members-viewer doesn't necessarily also hold. The tier
   *  filter dropdown just stays empty (no filter offered) rather than erroring if this fails. */
  protected readonly tiers = signal<TierDto[]>([]);

  protected readonly canViewFollowers = computed(() => this.permissionService.getGrantedPolicy('Eksabli.Followers.View'));

  private readonly followersLoaded = signal(false);
  protected readonly followers = signal<FollowerDto[]>([]);
  protected readonly followersTotalCount = signal(0);
  protected readonly followersLoading = signal(false);
  protected readonly followersFailed = signal(false);

  // --- Manual Point Adjustment modal (real, `PosAppService.ManualAdjustAsync`) ---
  protected readonly adjustModalOpen = signal(false);
  protected readonly isAdjusting = signal(false);
  protected adjustingMember: MemberDto | null = null;

  protected readonly adjustForm = new FormGroup({
    direction: new FormControl<'add' | 'remove'>('add', { nonNullable: true }),
    amount: new FormControl<number | null>(null, { validators: [Validators.required, Validators.min(1)] }),
    reason: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(256)] }),
  });

  ngOnInit(): void {
    this.loadTiers();
    this.load();
  }

  /** Null when neither name is set — template falls back to the localized "Unnamed customer" text
   *  rather than this helper mixing a raw name string with a localization key. */
  protected customerName(member: MemberDto | FollowerDto): string | null {
    const name = [member.firstName, member.lastName].filter(Boolean).join(' ').trim();
    return name || null;
  }

  protected initials(member: MemberDto | FollowerDto): string {
    const name = [member.firstName, member.lastName].filter(Boolean).join(' ').trim();
    if (!name) return '?';
    return name
      .split(' ')
      .filter(Boolean)
      .map((word) => word[0])
      .join('')
      .slice(0, 2)
      .toUpperCase();
  }

  protected statusLabelKey(status: MembershipStatus | undefined): string {
    return status === MembershipStatus.Frozen
      ? '::BusinessPanel:Customers:StatusFrozen'
      : '::BusinessPanel:Customers:StatusActive';
  }

  protected statusVariant(status: MembershipStatus | undefined): StatusBadgeVariant {
    return status === MembershipStatus.Frozen ? 'neutral' : 'success';
  }

  protected selectTab(tab: CustomersTab): void {
    this.activeTab.set(tab);
    if (tab === 'following' && !this.followersLoaded()) {
      this.loadFollowers();
    }
  }

  protected onSearchInput(value: string): void {
    this.filterText.set(value);
    this.pageIndex.set(0);
    this.load();
  }

  protected onTierFilterChange(event: Event): void {
    this.tierFilterValue.set((event.target as HTMLSelectElement).value);
    this.pageIndex.set(0);
    this.load();
  }

  protected onStatusFilterChange(event: Event): void {
    this.statusFilterValue.set((event.target as HTMLSelectElement).value);
    this.pageIndex.set(0);
    this.load();
  }

  protected goToPage(index: number): void {
    if (index < 0 || index >= this.totalPages()) return;
    this.pageIndex.set(index);
    this.load();
  }

  protected retry(): void {
    this.load();
  }

  protected retryFollowers(): void {
    this.loadFollowers();
  }

  protected openAdjustModal(member: MemberDto): void {
    this.adjustingMember = member;
    this.adjustForm.reset({ direction: 'add', amount: null, reason: '' });
    this.adjustModalOpen.set(true);
  }

  protected closeAdjustModal(): void {
    this.adjustModalOpen.set(false);
  }

  protected submitAdjust(): void {
    const member = this.adjustingMember;
    if (!member?.customerId || this.adjustForm.invalid) {
      this.adjustForm.markAllAsTouched();
      return;
    }

    const value = this.adjustForm.getRawValue();
    const amount = value.amount ?? 0;
    const points = value.direction === 'remove' ? -amount : amount;

    this.isAdjusting.set(true);
    this.posService.manualAdjust({ customerId: member.customerId, points, reason: value.reason || undefined }).subscribe({
      next: () => {
        this.isAdjusting.set(false);
        this.adjustModalOpen.set(false);
        this.toaster.success('::BusinessPanel:Customers:AdjustSuccessMessage');
        this.load();
      },
      // The real daily-cap/role/customer-not-joined errors all surface here as the backend's own
      // UserFriendlyException message via ABP's global HTTP error handling — no separate client-side
      // cap check duplicated (see this component's own file comment for why).
      error: () => {
        this.isAdjusting.set(false);
        this.toaster.error('::BusinessPanel:Customers:AdjustErrorMessage');
      },
    });
  }

  private loadTiers(): void {
    this.tiersService.getList({ sorting: 'minLifetimePoints asc', skipCount: 0, maxResultCount: 100 }).subscribe({
      next: (result) => this.tiers.set(result.items ?? []),
      error: () => undefined,
    });
  }

  private load(): void {
    this.isLoading.set(true);
    this.loadFailed.set(false);

    const status = this.statusFilterValue() === '' ? null : (Number(this.statusFilterValue()) as MembershipStatus);

    this.membershipsService
      .getMembers({
        filterText: this.filterText() || null,
        tierId: this.tierFilterValue() || null,
        status,
        sorting: undefined,
        skipCount: this.pageIndex() * this.pageSize,
        maxResultCount: this.pageSize,
      })
      .subscribe({
        next: (result) => {
          this.members.set(result.items ?? []);
          this.totalCount.set(result.totalCount ?? 0);
          this.isLoading.set(false);
        },
        error: () => {
          this.isLoading.set(false);
          this.loadFailed.set(true);
        },
      });
  }

  private loadFollowers(): void {
    this.followersLoading.set(true);
    this.followersFailed.set(false);
    this.followsService.getFollowers({ sorting: 'followedAt desc', skipCount: 0, maxResultCount: 50 }).subscribe({
      next: (result) => {
        this.followers.set(result.items ?? []);
        this.followersTotalCount.set(result.totalCount ?? 0);
        this.followersLoading.set(false);
        this.followersLoaded.set(true);
      },
      error: () => {
        this.followersLoading.set(false);
        this.followersFailed.set(true);
      },
    });
  }
}
