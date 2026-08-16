import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { MembershipsService } from '../../proxy/controllers/memberships.service';
import { ReportsService } from '../../proxy/controllers/reports.service';
import { CouponAuditService } from '../../proxy/controllers/coupon-audit.service';
import { PosService } from '../../proxy/controllers/pos.service';
import type { MemberDto } from '../../proxy/memberships/models';
import type { TransactionListItemDto } from '../../proxy/reports/models';
import type { CouponDto } from '../../proxy/rewards/models';
import { PointsTransactionType } from '../../proxy/wallets/points-transaction-type.enum';
import { PointsTransactionSource } from '../../proxy/wallets/points-transaction-source.enum';
import { CouponStatus } from '../../proxy/rewards/coupon-status.enum';
import { MembershipStatus } from '../../proxy/memberships/membership-status.enum';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { LoadingSpinnerComponent } from '../../shared/components/loading-spinner/loading-spinner.component';
import { ErrorStateComponent } from '../../shared/components/error-state/error-state.component';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { PaginationComponent } from '../../shared/components/pagination/pagination.component';
import { StatusBadgeComponent, StatusBadgeVariant } from '../../shared/components/status-badge/status-badge.component';
import { ModalComponent } from '../../shared/components/modal/modal.component';

type DetailTab = 'transactions' | 'coupons';

/**
 * Business Portal > Customer Details — the drill-down page `business-customers.component.ts`'s own
 * doc comment previously said wasn't real-buildable (`prototype/business/customer-details.html`).
 * Re-checked this session and closed each gap that comment listed, with small, justified backend
 * additions (no fabricated data, every field maps to a real column):
 * - **`MembershipAppService.GetMemberAsync(id)`** (new) — the single-row counterpart `GetMembersAsync`
 *   never had. Same real join (Membership → PointsWallet/Tier → CustomerProfile/IdentityUser), just for
 *   one row instead of the whole tenant. `GET /api/app/memberships/{id}`, same `Eksabli.Memberships.View`
 *   permission as the list.
 * - **`TransactionFilterDto.MembershipId`** (new) — `GetTransactionsListAsync` resolves it to the
 *   member's real `WalletId` server-side (`PointsTransaction` has no membership column of its own,
 *   same "derive it" shape `BranchId` already used there) before filtering.
 * - **`CouponAuditFilterDto.MembershipId`** (new) — `Coupon.MembershipId` was already a real column,
 *   just never filterable; now is.
 *
 * **Manual Point Adjustment** is real (`PosAppService.ManualAdjustAsync`) and was already wired up as a
 * per-row action on the Customers LIST page earlier this session (before this details page existed) —
 * intentionally duplicated here (not extracted into a shared component this pass) rather than risking a
 * regression on that already-working page for a refactor that wasn't asked for. Same real cap (20
 * adjustments/employee/day, a count — NOT the prototype's fabricated "200 pts" copy) and same
 * Owner/BranchManager-only gate enforced server-side, not re-derived client-side.
 *
 * Tier badge intentionally does NOT reuse the Customers list's index-based cycling color (that exists
 * to visually differentiate many rows in one table) — a single detail page just shows the real tier
 * name plainly.
 */
@Component({
  selector: 'app-business-customer-details',
  templateUrl: './business-customer-details.component.html',
  styleUrls: ['./business-customer-details.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    RouterLink,
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
export class BusinessCustomerDetailsComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly membershipsService = inject(MembershipsService);
  private readonly reportsService = inject(ReportsService);
  private readonly couponAuditService = inject(CouponAuditService);
  private readonly posService = inject(PosService);
  private readonly toaster = inject(ToasterService);

  protected readonly Status = MembershipStatus;
  protected readonly Type = PointsTransactionType;
  private readonly pageSize = 10;

  private membershipId: string | null = null;

  protected readonly member = signal<MemberDto | null>(null);
  protected readonly isLoading = signal(true);
  protected readonly loadFailed = signal(false);

  protected readonly activeTab = signal<DetailTab>('transactions');

  protected readonly transactions = signal<TransactionListItemDto[]>([]);
  protected readonly transactionsTotalCount = signal(0);
  protected readonly transactionsPageIndex = signal(0);
  protected readonly transactionsTotalPages = computed(() => Math.max(1, Math.ceil(this.transactionsTotalCount() / this.pageSize)));
  protected readonly transactionsLoading = signal(false);
  protected readonly transactionsFailed = signal(false);
  private transactionsLoaded = false;

  protected readonly coupons = signal<CouponDto[]>([]);
  protected readonly couponsTotalCount = signal(0);
  protected readonly couponsPageIndex = signal(0);
  protected readonly couponsTotalPages = computed(() => Math.max(1, Math.ceil(this.couponsTotalCount() / this.pageSize)));
  protected readonly couponsLoading = signal(false);
  protected readonly couponsFailed = signal(false);
  private couponsLoaded = false;

  // --- Manual Point Adjustment modal (real, PosAppService.ManualAdjustAsync — see file comment) ---
  protected readonly adjustModalOpen = signal(false);
  protected readonly isAdjusting = signal(false);

  protected readonly adjustForm = new FormGroup({
    direction: new FormControl<'add' | 'remove'>('add', { nonNullable: true }),
    amount: new FormControl<number | null>(null, { validators: [Validators.required, Validators.min(1)] }),
    reason: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(256)] }),
  });

  ngOnInit(): void {
    this.route.paramMap.subscribe((params) => {
      const id = params.get('id');
      if (!id) return;
      this.membershipId = id;
      this.load(id);
    });
  }

  protected retry(): void {
    if (this.membershipId) this.load(this.membershipId);
  }

  protected customerName(member: MemberDto): string | null {
    const name = [member.firstName, member.lastName].filter(Boolean).join(' ').trim();
    return name || null;
  }

  protected initials(member: MemberDto): string {
    const name = this.customerName(member);
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

  protected selectTab(tab: DetailTab): void {
    this.activeTab.set(tab);
    if (tab === 'transactions' && !this.transactionsLoaded) this.loadTransactions();
    if (tab === 'coupons' && !this.couponsLoaded) this.loadCoupons();
  }

  protected goToTransactionsPage(index: number): void {
    this.transactionsPageIndex.set(index);
    this.loadTransactions();
  }

  protected goToCouponsPage(index: number): void {
    this.couponsPageIndex.set(index);
    this.loadCoupons();
  }

  protected retryTransactions(): void {
    this.loadTransactions();
  }

  protected retryCoupons(): void {
    this.loadCoupons();
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

  protected sourceLabelKey(source: PointsTransactionSource | undefined): string {
    switch (source) {
      case PointsTransactionSource.Campaign:
        return '::BusinessPanel:Transactions:SourceCampaign';
      case PointsTransactionSource.Referral:
        return '::BusinessPanel:Transactions:SourceReferral';
      case PointsTransactionSource.Birthday:
        return '::BusinessPanel:Transactions:SourceBirthday';
      case PointsTransactionSource.Manual:
        return '::BusinessPanel:Transactions:SourceManual';
      case PointsTransactionSource.Reward:
        return '::BusinessPanel:Transactions:SourceReward';
      default:
        return '::BusinessPanel:Transactions:SourcePurchase';
    }
  }

  protected couponStatusLabelKey(status: CouponStatus | undefined): string {
    switch (status) {
      case CouponStatus.Redeemed:
        return '::BusinessPanel:Coupons:StatusRedeemed';
      case CouponStatus.Expired:
        return '::BusinessPanel:Coupons:StatusExpired';
      case CouponStatus.Cancelled:
        return '::BusinessPanel:Coupons:StatusCancelled';
      default:
        return '::BusinessPanel:Coupons:StatusIssued';
    }
  }

  protected couponStatusVariant(status: CouponStatus | undefined): StatusBadgeVariant {
    switch (status) {
      case CouponStatus.Redeemed:
        return 'neutral';
      case CouponStatus.Expired:
      case CouponStatus.Cancelled:
        return 'danger';
      default:
        return 'success';
    }
  }

  protected openAdjustModal(): void {
    this.adjustForm.reset({ direction: 'add', amount: null, reason: '' });
    this.adjustModalOpen.set(true);
  }

  protected closeAdjustModal(): void {
    this.adjustModalOpen.set(false);
  }

  protected submitAdjust(): void {
    const member = this.member();
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
        if (this.membershipId) this.load(this.membershipId);
        this.transactionsLoaded = false;
        if (this.activeTab() === 'transactions') this.loadTransactions();
      },
      error: () => {
        this.isAdjusting.set(false);
        this.toaster.error('::BusinessPanel:Customers:AdjustErrorMessage');
      },
    });
  }

  private load(id: string): void {
    this.isLoading.set(true);
    this.loadFailed.set(false);
    this.transactionsLoaded = false;
    this.couponsLoaded = false;

    this.membershipsService.get(id).subscribe({
      next: (result) => {
        this.member.set(result);
        this.isLoading.set(false);
        this.selectTab(this.activeTab());
      },
      error: () => {
        this.isLoading.set(false);
        this.loadFailed.set(true);
      },
    });
  }

  private loadTransactions(): void {
    if (!this.membershipId) return;
    this.transactionsLoading.set(true);
    this.transactionsFailed.set(false);
    this.reportsService
      .getTransactionsList({
        membershipId: this.membershipId,
        type: null,
        branchId: null,
        staffId: null,
        from: null,
        to: null,
        sorting: undefined,
        skipCount: this.transactionsPageIndex() * this.pageSize,
        maxResultCount: this.pageSize,
      })
      .subscribe({
        next: (result) => {
          this.transactions.set(result.items ?? []);
          this.transactionsTotalCount.set(result.totalCount ?? 0);
          this.transactionsLoading.set(false);
          this.transactionsLoaded = true;
        },
        error: () => {
          this.transactionsLoading.set(false);
          this.transactionsFailed.set(true);
        },
      });
  }

  private loadCoupons(): void {
    if (!this.membershipId) return;
    this.couponsLoading.set(true);
    this.couponsFailed.set(false);
    this.couponAuditService
      .getList({
        membershipId: this.membershipId,
        status: null,
        branchId: null,
        sorting: 'issuedAt desc',
        skipCount: this.couponsPageIndex() * this.pageSize,
        maxResultCount: this.pageSize,
      })
      .subscribe({
        next: (result) => {
          this.coupons.set(result.items ?? []);
          this.couponsTotalCount.set(result.totalCount ?? 0);
          this.couponsLoading.set(false);
          this.couponsLoaded = true;
        },
        error: () => {
          this.couponsLoading.set(false);
          this.couponsFailed.set(true);
        },
      });
  }
}
