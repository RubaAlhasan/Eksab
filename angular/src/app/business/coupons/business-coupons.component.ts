import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { LocalizationPipe, PermissionService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { CouponAuditService } from '../../proxy/controllers/coupon-audit.service';
import { BranchesService } from '../../proxy/controllers/branches.service';
import { EmployeeAssignmentsService } from '../../proxy/controllers/employee-assignments.service';
import { MembershipsService } from '../../proxy/controllers/memberships.service';
import type { CouponDto } from '../../proxy/rewards/models';
import { CouponStatus } from '../../proxy/rewards/coupon-status.enum';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { LoadingSpinnerComponent } from '../../shared/components/loading-spinner/loading-spinner.component';
import { ErrorStateComponent } from '../../shared/components/error-state/error-state.component';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { PaginationComponent } from '../../shared/components/pagination/pagination.component';
import { StatusBadgeComponent, StatusBadgeVariant } from '../../shared/components/status-badge/status-badge.component';

/**
 * Business Portal > Coupons — mirrors prototype/business/coupons.html's audit trail, built against
 * `CouponAuditAppService` (`GetListAsync`/`GetDownloadTokenAsync`/`GetListAsExcelFileAsync`, whole
 * controller gated on `Eksabli.Rewards.Default` — same permission as the Rewards page itself, since a
 * coupon audit trail is really "Rewards" data). No backend changes needed; every endpoint and its
 * proxy already existed.
 *
 * Real fields, matching the prototype closely:
 * - Code, Status (`Issued`/`Redeemed`/`Expired`/`Cancelled` — exactly the prototype's own four
 *   options), Reward name — all real, and `CouponDto.RewardNameEn`/`RewardNameAr` are already resolved
 *   server-side by `GetListAsync` itself (no client join needed for that one).
 * - **Member name IS resolved, but client-side** — `CouponDto` only carries a bare `MembershipId`,
 *   no name. Reused `MembershipsService.getMembers()` (the same real endpoint Customers' Members tab
 *   already calls) as a bulk `membershipId -> name` lookup — best-effort, not a new backend call
 *   invented for this page.
 * - **Branch / Redeemed By are also resolved client-side** — `CouponDto.RedeemedBranchId`/
 *   `RedeemedByEmployeeId` are bare ids too. Branch name via the same bulk `BranchesService.getList`
 *   lookup Employees/Branches already use; "Redeemed By" resolves `RedeemedByEmployeeId` (a raw
 *   `IdentityUser.Id` — confirmed by reading `PosAppService.ConfirmRedemptionAsync`, which passes
 *   `CurrentUser.GetId()` as the employee id) against the same `EmployeeAssignmentsService.getList()`
 *   bulk lookup the Employees page uses, matching on `userId` -> `userEmail` (no display name exists
 *   for staff anywhere in this codebase, same finding as the Users/Employees pages — email is shown).
 * - **Date column** shows `RedeemedAt` when the coupon has been redeemed, else `IssuedAt` — both real
 *   fields on `CouponDto`, picking whichever actually happened is more informative than the
 *   prototype's own single flat "date" field.
 * - Status/Branch filters are real (`CouponAuditFilterDto.Status`/`.BranchId`, server-side). **No code
 *   search** — `[MISSING BACKEND CAPABILITY]`, `CouponAuditFilterDto` has no `filterText` field, same
 *   gap already documented on Support Tickets/Employees.
 * - **Export IS real** — the same two-step token-gated Excel-export pattern documented in CLAUDE.md
 *   (`GetDownloadTokenAsync()` then `GetListAsExcelFileAsync()`, `[AllowAnonymous]` on the file-stream
 *   endpoint itself since the token is the real auth check, same shape as every other Excel export in
 *   this app) — the FIRST real blob-download implemented in this Angular app this session; downloaded
 *   directly via a `Blob`/`ObjectURL` anchor click, no server-side "generated report" persistence
 *   (unlike the prototype's own fake "Recent Exports" history table on `reports.html`, deliberately
 *   not replicated here — there's nothing to list, each export is generated fresh on demand).
 */
@Component({
  selector: 'app-business-coupons',
  templateUrl: './business-coupons.component.html',
  styleUrls: ['./business-coupons.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    LocalizationPipe,
    PageHeaderComponent,
    LoadingSpinnerComponent,
    ErrorStateComponent,
    EmptyStateComponent,
    PaginationComponent,
    StatusBadgeComponent,
  ],
})
export class BusinessCouponsComponent implements OnInit {
  private readonly couponAuditService = inject(CouponAuditService);
  private readonly branchesService = inject(BranchesService);
  private readonly employeeAssignmentsService = inject(EmployeeAssignmentsService);
  private readonly membershipsService = inject(MembershipsService);
  private readonly toaster = inject(ToasterService);
  private readonly permissionService = inject(PermissionService);

  protected readonly Status = CouponStatus;
  private readonly pageSize = 10;

  protected readonly coupons = signal<CouponDto[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly isLoading = signal(true);
  protected readonly loadFailed = signal(false);
  protected readonly statusFilterValue = signal('');
  protected readonly branchFilterValue = signal('');
  protected readonly pageIndex = signal(0);
  protected readonly totalPages = computed(() => Math.max(1, Math.ceil(this.totalCount() / this.pageSize)));

  protected readonly branches = signal<{ id: string; name: string }[]>([]);
  private readonly branchNameById = signal<Map<string, string>>(new Map());
  private readonly employeeEmailByUserId = signal<Map<string, string>>(new Map());
  private readonly memberNameByMembershipId = signal<Map<string, string>>(new Map());

  protected readonly canExport = computed(() => this.permissionService.getGrantedPolicy('Eksabli.Rewards.Default'));
  protected readonly isExporting = signal(false);

  ngOnInit(): void {
    this.loadBranches();
    this.loadEmployees();
    this.loadMembers();
    this.load();
  }

  protected retry(): void {
    this.load();
  }

  protected onStatusFilterChange(event: Event): void {
    this.statusFilterValue.set((event.target as HTMLSelectElement).value);
    this.pageIndex.set(0);
    this.load();
  }

  protected onBranchFilterChange(event: Event): void {
    this.branchFilterValue.set((event.target as HTMLSelectElement).value);
    this.pageIndex.set(0);
    this.load();
  }

  protected goToPage(index: number): void {
    if (index < 0 || index >= this.totalPages()) return;
    this.pageIndex.set(index);
    this.load();
  }

  protected memberName(coupon: CouponDto): string {
    if (!coupon.membershipId) return '—';
    return this.memberNameByMembershipId().get(coupon.membershipId) ?? '—';
  }

  protected branchName(coupon: CouponDto): string {
    if (!coupon.redeemedBranchId) return '—';
    return this.branchNameById().get(coupon.redeemedBranchId) ?? '—';
  }

  protected redeemedByEmail(coupon: CouponDto): string {
    if (!coupon.redeemedByEmployeeId) return '—';
    return this.employeeEmailByUserId().get(coupon.redeemedByEmployeeId) ?? '—';
  }

  protected displayDate(coupon: CouponDto): string | undefined {
    return coupon.redeemedAt ?? coupon.issuedAt;
  }

  protected statusLabelKey(status: CouponStatus | undefined): string {
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

  protected statusVariant(status: CouponStatus | undefined): StatusBadgeVariant {
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

  protected exportExcel(): void {
    this.isExporting.set(true);
    this.couponAuditService.getDownloadToken().subscribe({
      next: (tokenResult) => {
        const status = this.statusFilterValue() === '' ? null : (Number(this.statusFilterValue()) as CouponStatus);
        this.couponAuditService
          .getListAsExcelFile({
            downloadToken: tokenResult.token,
            status,
            branchId: this.branchFilterValue() || null,
            sorting: undefined,
          })
          .subscribe({
            next: (blob) => {
              this.isExporting.set(false);
              this.downloadBlob(blob, 'Coupons.xlsx');
            },
            error: () => {
              this.isExporting.set(false);
              this.toaster.error('::BusinessPanel:Coupons:ExportErrorMessage');
            },
          });
      },
      error: () => {
        this.isExporting.set(false);
        this.toaster.error('::BusinessPanel:Coupons:ExportErrorMessage');
      },
    });
  }

  private downloadBlob(blob: Blob, fileName: string): void {
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName;
    anchor.click();
    URL.revokeObjectURL(url);
  }

  private load(): void {
    this.isLoading.set(true);
    this.loadFailed.set(false);

    const status = this.statusFilterValue() === '' ? null : (Number(this.statusFilterValue()) as CouponStatus);

    this.couponAuditService
      .getList({
        status,
        branchId: this.branchFilterValue() || null,
        sorting: 'issuedAt desc',
        skipCount: this.pageIndex() * this.pageSize,
        maxResultCount: this.pageSize,
      })
      .subscribe({
        next: (result) => {
          this.coupons.set(result.items ?? []);
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
        const map = new Map<string, string>();
        for (const employee of result.items ?? []) {
          if (employee.userId && employee.userEmail) map.set(employee.userId, employee.userEmail);
        }
        this.employeeEmailByUserId.set(map);
      },
      error: () => undefined,
    });
  }

  private loadMembers(): void {
    this.membershipsService
      .getMembers({ filterText: null, tierId: null, status: null, sorting: undefined, skipCount: 0, maxResultCount: 500 })
      .subscribe({
        next: (result) => {
          const map = new Map<string, string>();
          for (const member of result.items ?? []) {
            const name = [member.firstName, member.lastName].filter(Boolean).join(' ').trim();
            if (member.id && name) map.set(member.id, name);
          }
          this.memberNameByMembershipId.set(map);
        },
        error: () => undefined,
      });
  }
}
