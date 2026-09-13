import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { AdminUsersService } from '../../proxy/controllers/admin-users.service';
import type { AdminCustomerDetailDto, AdminCustomerMembershipDto } from '../../proxy/platform/models';
import type { PointsTransactionDto } from '../../proxy/wallets/models';
import { PointsTransactionType } from '../../proxy/wallets/points-transaction-type.enum';
import { PointsTransactionSource } from '../../proxy/wallets/points-transaction-source.enum';
import { MembershipStatus } from '../../proxy/memberships/membership-status.enum';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { LoadingSpinnerComponent } from '../../shared/components/loading-spinner/loading-spinner.component';
import { ErrorStateComponent } from '../../shared/components/error-state/error-state.component';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { PaginationComponent } from '../../shared/components/pagination/pagination.component';
import { StatusBadgeComponent, StatusBadgeVariant } from '../../shared/components/status-badge/status-badge.component';

/**
 * Admin Portal > Users > Customer Details — the drill-down `admin-users.component.html` never had
 * (its own list page previously had no click-through anywhere). Customer-only: a Staff row from the
 * list has no wallet/membership concept to show here, so `admin-users.component.html` only links
 * Customer rows to this route (see that template's own guard).
 *
 * Backed by two new endpoints added this session (`AdminUserAppService.GetCustomerDetailAsync`,
 * `GET /api/app/admin-users/{customerId}`, and `.GetCustomerTransactionsAsync`,
 * `GET /api/app/admin-users/memberships/{membershipId}/transactions?tenantId=...`) — both
 * `Eksabli.Users.View`, same permission the list page already requires, no new grant needed.
 *
 * **Shape mirrors the two-realm identity model directly**: one customer can have N independent
 * business memberships, each with its own wallet — `AdminCustomerDetailDto.memberships` is a list, not
 * a single balance. Balances/lifetime totals are deliberately never summed across rows (each business's
 * points are its own currency — see `AdminCustomerMembershipDto`'s own backend comment); every number
 * shown is scoped to the one business its row belongs to.
 *
 * **Expandable-row pattern for transactions, not a routed sub-page** — same shape as
 * `admin-subscriptions.component.ts`'s Invoices/Payments drill-down (`expandedId`/`toggleExpand`,
 * including its stale-response guard on `expandedMembershipId()` before applying a slow response).
 * Chosen for the same reason that page gives: a membership's ledger doesn't carry enough of its own
 * identity to deserve a full route, and this avoids a second network round-trip's data going stale if
 * the admin has since collapsed/switched to a different business.
 *
 * Transaction type/source labels reuse the Business Portal's own `BusinessPanel:Transactions:*`
 * localization keys rather than a parallel `AdminPanel:*` set — same enum, same meaning, in both
 * places; duplicating the strings would just be two copies to keep in sync for no reason.
 */
@Component({
  selector: 'app-admin-user-details',
  templateUrl: './admin-user-details.component.html',
  styleUrls: ['./admin-user-details.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    RouterLink,
    LocalizationPipe,
    PageHeaderComponent,
    LoadingSpinnerComponent,
    ErrorStateComponent,
    EmptyStateComponent,
    PaginationComponent,
    StatusBadgeComponent,
  ],
})
export class AdminUserDetailsComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly usersService = inject(AdminUsersService);

  protected readonly MembershipStatus = MembershipStatus;
  protected readonly Type = PointsTransactionType;
  private readonly pageSize = 10;

  private customerId: string | null = null;

  protected readonly customer = signal<AdminCustomerDetailDto | null>(null);
  protected readonly isLoading = signal(true);
  protected readonly notFound = signal(false);
  protected readonly loadFailed = signal(false);

  // --- Expanded membership's transaction ledger ---
  protected readonly expandedMembershipId = signal<string | null>(null);
  protected readonly transactions = signal<PointsTransactionDto[]>([]);
  protected readonly transactionsTotalCount = signal(0);
  protected readonly transactionsPageIndex = signal(0);
  protected readonly transactionsTotalPages = computed(() => Math.max(1, Math.ceil(this.transactionsTotalCount() / this.pageSize)));
  protected readonly transactionsLoading = signal(false);
  protected readonly transactionsFailed = signal(false);

  ngOnInit(): void {
    this.route.paramMap.subscribe((params) => {
      const id = params.get('id');
      if (!id) return;
      this.customerId = id;
      this.load(id);
    });
  }

  protected retry(): void {
    if (this.customerId) this.load(this.customerId);
  }

  protected displayName(customer: AdminCustomerDetailDto): string | null {
    const name = [customer.firstName, customer.lastName].filter(Boolean).join(' ').trim();
    return name || null;
  }

  protected initials(customer: AdminCustomerDetailDto): string {
    const name = this.displayName(customer);
    if (name) {
      return name
        .split(' ')
        .filter(Boolean)
        .map((word) => word[0])
        .join('')
        .slice(0, 2)
        .toUpperCase();
    }
    return (customer.contact ?? '?').slice(0, 2).toUpperCase();
  }

  protected statusLabelKey(isActive: boolean): string {
    return isActive ? '::AdminPanel:Users:StatusActive' : '::AdminPanel:Users:StatusInactive';
  }

  protected statusVariant(isActive: boolean): StatusBadgeVariant {
    return isActive ? 'success' : 'neutral';
  }

  protected membershipStatusLabelKey(status: MembershipStatus | undefined): string {
    return status === MembershipStatus.Frozen ? '::BusinessPanel:Customers:StatusFrozen' : '::BusinessPanel:Customers:StatusActive';
  }

  protected membershipStatusVariant(status: MembershipStatus | undefined): StatusBadgeVariant {
    return status === MembershipStatus.Frozen ? 'neutral' : 'success';
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
      case PointsTransactionSource.Tier:
        return '::BusinessPanel:Transactions:SourceTier';
      default:
        return '::BusinessPanel:Transactions:SourcePurchase';
    }
  }

  protected toggleMembership(membership: AdminCustomerMembershipDto): void {
    const id = membership.membershipId;
    if (!id) return;

    if (this.expandedMembershipId() === id) {
      this.expandedMembershipId.set(null);
      return;
    }

    this.expandedMembershipId.set(id);
    this.transactionsPageIndex.set(0);
    this.loadTransactions(membership);
  }

  protected retryTransactions(membership: AdminCustomerMembershipDto): void {
    this.loadTransactions(membership);
  }

  protected goToTransactionsPage(index: number, membership: AdminCustomerMembershipDto): void {
    this.transactionsPageIndex.set(index);
    this.loadTransactions(membership);
  }

  private load(id: string): void {
    this.isLoading.set(true);
    this.notFound.set(false);
    this.loadFailed.set(false);
    this.expandedMembershipId.set(null);

    this.usersService.getCustomerDetail(id).subscribe({
      next: (result) => {
        this.customer.set(result);
        this.isLoading.set(false);
      },
      error: (err: HttpErrorResponse) => {
        this.isLoading.set(false);
        if (err.status === 404) {
          this.notFound.set(true);
        } else {
          this.loadFailed.set(true);
        }
      },
    });
  }

  private loadTransactions(membership: AdminCustomerMembershipDto): void {
    const membershipId = membership.membershipId;
    const tenantId = membership.tenantId;
    if (!membershipId || !tenantId) return;

    this.transactionsLoading.set(true);
    this.transactionsFailed.set(false);

    this.usersService
      .getCustomerTransactions(membershipId, tenantId, {
        sorting: undefined,
        skipCount: this.transactionsPageIndex() * this.pageSize,
        maxResultCount: this.pageSize,
      })
      .subscribe({
        next: (result) => {
          // Ignore a slow response for a membership the admin has since collapsed/switched away from —
          // same guard admin-subscriptions.component.ts uses for its own Invoices/Payments expansion.
          if (this.expandedMembershipId() !== membershipId) return;
          this.transactions.set(result.items ?? []);
          this.transactionsTotalCount.set(result.totalCount ?? 0);
          this.transactionsLoading.set(false);
        },
        error: () => {
          if (this.expandedMembershipId() !== membershipId) return;
          this.transactionsLoading.set(false);
          this.transactionsFailed.set(true);
        },
      });
  }
}
