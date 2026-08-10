import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { LocalizationPipe, PermissionService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { ReportsService } from '../../proxy/controllers/reports.service';
import { BranchesService } from '../../proxy/controllers/branches.service';
import { EmployeeAssignmentsService } from '../../proxy/controllers/employee-assignments.service';
import type { TransactionListItemDto } from '../../proxy/reports/models';
import { PointsTransactionType } from '../../proxy/wallets/points-transaction-type.enum';
import { PointsTransactionSource } from '../../proxy/wallets/points-transaction-source.enum';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { LoadingSpinnerComponent } from '../../shared/components/loading-spinner/loading-spinner.component';
import { ErrorStateComponent } from '../../shared/components/error-state/error-state.component';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { PaginationComponent } from '../../shared/components/pagination/pagination.component';
import { StatusBadgeComponent, StatusBadgeVariant } from '../../shared/components/status-badge/status-badge.component';
import { downloadBlob } from '../../shared/utils/download-blob';

function startOfMonth(date: Date): Date {
  return new Date(date.getFullYear(), date.getMonth(), 1);
}

// `date.toISOString()` converts to UTC first — for any timezone AHEAD of UTC, local midnight (what
// `startOfMonth`/"today" actually mean to the user) rolls back to the previous day once converted,
// so an `<input type="date">` bound to that string silently shows the wrong default day. A real bug
// caught live (this session's own browser walkthrough, timezone UTC+something showed "07/31" instead
// of "08/01" as the month-start default) — use local date PARTS directly, never `.toISOString()`, to
// build a `yyyy-MM-dd` value from a local `Date`.
function toDateInputValue(date: Date): string {
  const year = date.getFullYear();
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const day = String(date.getDate()).padStart(2, '0');
  return `${year}-${month}-${day}`;
}

/**
 * Business Portal > Transactions — mirrors prototype/business/transactions.html's live, filterable
 * ledger table (Type/Branch/Staff/date filters, paginated grid) against a real backend endpoint added
 * alongside this page, `ReportsAppService.GetTransactionsListAsync` (`GET /api/app/report/transactions`,
 * gated on the same `Eksabli.Reports.Default` every other read-only report endpoint here uses — Export
 * stays its own tighter `Eksabli.Reports.Export`).
 *
 * - **Branch has no real column on `PointsTransaction`** — the backend derives it from
 *   `CreatedByEmployeeId -> EmployeeAssignment.BranchId` (same soft-reference space the entity already
 *   documents). Rows with no staff attribution (customer/system-triggered earns) or a staff member with
 *   all-branch access simply resolve to no branch (shown as "—"), which is the honest answer rather
 *   than the prototype's own always-populated demo column.
 * - **Customer name IS resolved server-side** (`CustomerFirstName`/`CustomerLastName`, joined through
 *   `PointsWallet -> Membership -> CustomerProfile`, the same join `GetTransactionsAsExcelFileAsync`
 *   already did for the Excel export) — unlike Coupons/Customers, no client-side bulk lookup needed for
 *   that column.
 * - **Branch/Staff names are resolved client-side**, same bulk `BranchesService.getList()` /
 *   `EmployeeAssignmentsService.getList()` lookups Coupons already uses — `TransactionListItemDto` only
 *   carries bare ids (no display name exists for staff anywhere in this codebase; email is shown).
 * - **The date range doubles as both the table filter and the Excel export range** — same `from`/`to`
 *   form this page already had for export, reused rather than duplicating a second date control the way
 *   the prototype's own single (unwired) date input would have implied.
 * - **Export IS real**, unchanged from before: the two-step token-gated Excel-export pattern documented
 *   in CLAUDE.md (`GetTransactionsDownloadTokenAsync()` -> `GetTransactionsAsExcelFileAsync()`,
 *   `[AllowAnonymous]` on the file-stream endpoint since the token is the real auth check).
 */
@Component({
  selector: 'app-business-transactions',
  templateUrl: './business-transactions.component.html',
  styleUrls: ['./business-transactions.component.scss'],
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
  ],
})
export class BusinessTransactionsComponent implements OnInit {
  private readonly reportsService = inject(ReportsService);
  private readonly branchesService = inject(BranchesService);
  private readonly employeeAssignmentsService = inject(EmployeeAssignmentsService);
  private readonly toaster = inject(ToasterService);
  private readonly permissionService = inject(PermissionService);

  protected readonly Type = PointsTransactionType;
  private readonly pageSize = 10;

  protected readonly transactions = signal<TransactionListItemDto[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly isLoading = signal(true);
  protected readonly loadFailed = signal(false);
  protected readonly typeFilterValue = signal('');
  protected readonly branchFilterValue = signal('');
  protected readonly staffFilterValue = signal('');
  protected readonly pageIndex = signal(0);
  protected readonly totalPages = computed(() => Math.max(1, Math.ceil(this.totalCount() / this.pageSize)));

  protected readonly branches = signal<{ id: string; name: string }[]>([]);
  private readonly branchNameById = signal<Map<string, string>>(new Map());
  protected readonly employees = signal<{ userId: string; userEmail: string }[]>([]);
  private readonly employeeEmailByUserId = signal<Map<string, string>>(new Map());

  protected readonly canExport = computed(() => this.permissionService.getGrantedPolicy('Eksabli.Reports.Export'));
  protected readonly isExporting = signal(false);

  protected readonly form = new FormGroup({
    from: new FormControl(toDateInputValue(startOfMonth(new Date())), { nonNullable: true, validators: [Validators.required] }),
    to: new FormControl(toDateInputValue(new Date()), { nonNullable: true, validators: [Validators.required] }),
  });

  ngOnInit(): void {
    this.loadBranches();
    this.loadEmployees();
    this.load();
  }

  protected retry(): void {
    this.load();
  }

  protected onTypeFilterChange(event: Event): void {
    this.typeFilterValue.set((event.target as HTMLSelectElement).value);
    this.pageIndex.set(0);
    this.load();
  }

  protected onBranchFilterChange(event: Event): void {
    this.branchFilterValue.set((event.target as HTMLSelectElement).value);
    this.pageIndex.set(0);
    this.load();
  }

  protected onStaffFilterChange(event: Event): void {
    this.staffFilterValue.set((event.target as HTMLSelectElement).value);
    this.pageIndex.set(0);
    this.load();
  }

  protected onDateRangeChange(): void {
    if (this.form.invalid) return;
    this.pageIndex.set(0);
    this.load();
  }

  protected goToPage(index: number): void {
    if (index < 0 || index >= this.totalPages()) return;
    this.pageIndex.set(index);
    this.load();
  }

  protected customerName(txn: TransactionListItemDto): string {
    const name = [txn.customerFirstName, txn.customerLastName].filter(Boolean).join(' ').trim();
    return name || '—';
  }

  protected branchName(txn: TransactionListItemDto): string {
    if (!txn.branchId) return '—';
    return this.branchNameById().get(txn.branchId) ?? '—';
  }

  protected staffEmail(txn: TransactionListItemDto): string {
    if (!txn.staffId) return '—';
    return this.employeeEmailByUserId().get(txn.staffId) ?? '—';
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

  protected exportExcel(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    this.isExporting.set(true);
    this.reportsService.getTransactionsDownloadToken().subscribe({
      next: (tokenResult) => {
        this.reportsService
          .getTransactionsAsExcelFile({
            downloadToken: tokenResult.token ?? '',
            from: new Date(value.from).toISOString(),
            // Include the whole "to" day, not just its midnight instant.
            to: new Date(`${value.to}T23:59:59`).toISOString(),
          })
          .subscribe({
            next: (blob) => {
              this.isExporting.set(false);
              downloadBlob(blob, `Transactions_${value.from}_${value.to}.xlsx`);
            },
            error: () => {
              this.isExporting.set(false);
              this.toaster.error('::BusinessPanel:Transactions:ExportErrorMessage');
            },
          });
      },
      error: () => {
        this.isExporting.set(false);
        this.toaster.error('::BusinessPanel:Transactions:ExportErrorMessage');
      },
    });
  }

  private load(): void {
    this.isLoading.set(true);
    this.loadFailed.set(false);

    const value = this.form.getRawValue();
    const type = this.typeFilterValue() === '' ? null : (Number(this.typeFilterValue()) as PointsTransactionType);

    this.reportsService
      .getTransactionsList({
        type,
        branchId: this.branchFilterValue() || null,
        staffId: this.staffFilterValue() || null,
        from: value.from ? new Date(value.from).toISOString() : null,
        // Include the whole "to" day, not just its midnight instant — same reasoning as exportExcel().
        to: value.to ? new Date(`${value.to}T23:59:59`).toISOString() : null,
        sorting: undefined,
        skipCount: this.pageIndex() * this.pageSize,
        maxResultCount: this.pageSize,
      })
      .subscribe({
        next: (result) => {
          this.transactions.set(result.items ?? []);
          this.totalCount.set(result.totalCount ?? 0);
          this.isLoading.set(false);
        },
        error: () => {
          this.isLoading.set(false);
          this.loadFailed.set(true);
        },
      });
  }

  private loadBranches(): void {
    this.branchesService.getList({ sorting: 'name asc', skipCount: 0, maxResultCount: 100 }).subscribe({
      next: (result) => {
        const items = (result.items ?? []).filter((b) => b.id && b.name) as { id: string; name: string }[];
        this.branches.set(items);
        this.branchNameById.set(new Map(items.map((b) => [b.id, b.name])));
      },
      error: () => undefined,
    });
  }

  private loadEmployees(): void {
    this.employeeAssignmentsService.getList({ skipCount: 0, maxResultCount: 500 }).subscribe({
      next: (result) => {
        const items = (result.items ?? []).filter((e) => e.userId && e.userEmail) as { userId: string; userEmail: string }[];
        this.employees.set(items);
        this.employeeEmailByUserId.set(new Map(items.map((e) => [e.userId, e.userEmail])));
      },
      error: () => undefined,
    });
  }
}
