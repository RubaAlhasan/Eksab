import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { LocalizationPipe, PermissionService } from '@abp/ng.core';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { EmployeeAssignmentsService } from '../../proxy/controllers/employee-assignments.service';
import { BranchesService } from '../../proxy/controllers/branches.service';
import type { EmployeeAssignmentDto } from '../../proxy/employee-assignments/models';
import type { BranchDto } from '../../proxy/branches/models';
import { EmployeeRole } from '../../proxy/employee-assignments/employee-role.enum';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { LoadingSpinnerComponent } from '../../shared/components/loading-spinner/loading-spinner.component';
import { ErrorStateComponent } from '../../shared/components/error-state/error-state.component';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { PaginationComponent } from '../../shared/components/pagination/pagination.component';
import { ModalComponent } from '../../shared/components/modal/modal.component';

/**
 * Business Portal > Employees — tenant-realm data (business staff), mirrors prototype/business/
 * employees.html, mounted at `/business/employees` inside the Business Portal shell
 * (`BusinessLayoutComponent`), gated on `businessRealmGuard` (core/guards/business.guard.ts — real
 * tenant-resolution, see that file's comment).
 *
 * Routing history: briefly lived at `/admin/employees` (folded into the Admin Portal shell) — moved
 * here at the user's explicit "must be under business/employees" + "when login business show page
 * related to business like prototype". See business-customers.component.ts's file comment for the
 * fuller history (same reasoning applies to both pages).
 *
 * Real API surface, nothing invented: `EmployeeAssignmentsController` (whole controller gated on
 * `Eksabli.EmployeeAssignments.Default`, no separate granular read permission — same "Default gates
 * the whole controller" shape as Billing/SupportTickets) — `GetListAsync`/`InviteAsync`/`UpdateAsync`/
 * `RemoveAsync` all already existed before this session; no backend changes needed. Proxy
 * (`proxy/employee-assignments/*`, `proxy/branches/*`) was already fully generated from an earlier
 * `abp generate-proxy` run — nothing hand-edited here either.
 *
 * Fields, deliberately different from the prototype's exact shape where the data isn't real:
 * - No separate "Name" column — confirmed (same search done for the Admin Users page)
 *   `IdentityUser.Name`/`.Surname` are never populated anywhere in this codebase for a staff account
 *   (`EmployeeAssignmentAppService.InviteAsync` and the owner-seeding path in `BusinessAppService
 *   .RegisterAsync` both only ever set username/email). The identity cell shows the real email instead
 *   of inventing a name or showing a duplicate "Unnamed" column next to an otherwise-identical email
 *   column — the prototype's own separate "Name"/"Email" columns would be redundant here.
 * - No "Status" column — the prototype shows Active/Invited/Suspended, none of which are real:
 *   `InviteAsync` creates a fully active account immediately (no pending-invite state exists anywhere
 *   in the domain, same finding as the Users page), and there is no soft-suspend — `RemoveAsync`
 *   deletes the `EmployeeAssignment` row outright, which is genuinely also the exact mechanism that
 *   revokes POS/staff access (`PosAppService`'s own role check reads this same table). Every row shown
 *   is, by construction, an active staff member — a static "Active" badge on every row would carry no
 *   real information, so it's omitted rather than shown as decoration.
 * - Branch resolves via a real bulk `BranchesService.getList` lookup (best-effort, same "load a
 *   bounded batch, fall back gracefully" pattern used throughout); a null `BranchId` is a real,
 *   documented domain state (`EmployeeAssignment.BranchId`'s own comment: "null = access to all
 *   branches"), not a missing value — shown as "All branches", not an em dash.
 * - Role dropdown in the Invite modal deliberately excludes "Owner" (matches the prototype's own
 *   choice) — there's no invite-as-owner flow; `EmployeeRole.Owner` is only ever set once, at business
 *   registration (`BusinessAppService.RegisterAsync`).
 * - The Owner's own row never shows a Revoke action (matches the prototype's exact real safeguard,
 *   `e.role !== 'Business Owner'`) — revoking your own only Owner assignment would be a same kind of
 *   footgun the prototype itself already guards against.
 * - `[MISSING BACKEND CAPABILITY]`, deliberately not built: no search/filter box (`PagedAndSortedResult
 *   RequestDto` has no `filterText` field for this endpoint, same gap already documented on the Support
 *   Tickets queue); no edit-role/reassign-branch UI, even though `UpdateAsync` exists server-side — the
 *   prototype itself doesn't expose one either (no edit button in its table), so this matches prototype
 *   scope rather than being a gap this page introduces.
 */
@Component({
  selector: 'app-business-employees',
  templateUrl: './business-employees.component.html',
  styleUrls: ['./business-employees.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    LocalizationPipe,
    PageHeaderComponent,
    LoadingSpinnerComponent,
    ErrorStateComponent,
    EmptyStateComponent,
    PaginationComponent,
    ModalComponent,
  ],
})
export class BusinessEmployeesComponent implements OnInit {
  private readonly employeesService = inject(EmployeeAssignmentsService);
  private readonly branchesService = inject(BranchesService);
  private readonly confirmation = inject(ConfirmationService);
  private readonly toaster = inject(ToasterService);
  private readonly permissionService = inject(PermissionService);

  protected readonly Role = EmployeeRole;
  private readonly pageSize = 10;

  protected readonly employees = signal<EmployeeAssignmentDto[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly isLoading = signal(true);
  protected readonly loadFailed = signal(false);
  protected readonly pageIndex = signal(0);
  protected readonly totalPages = computed(() => Math.max(1, Math.ceil(this.totalCount() / this.pageSize)));

  /** Best-effort bulk lookup — `BranchesController` is gated on its own `Eksabli.Branches.Default`
   *  (a viewer with `EmployeeAssignments.Default` doesn't necessarily also hold it); the Branch column
   *  just falls back to "All branches"/a raw id rather than erroring. */
  protected readonly branches = signal<BranchDto[]>([]);
  private readonly branchNameById = signal<Map<string, string>>(new Map());

  protected readonly canInvite = computed(() => this.permissionService.getGrantedPolicy('Eksabli.EmployeeAssignments.Create'));
  protected readonly canRevoke = computed(() => this.permissionService.getGrantedPolicy('Eksabli.EmployeeAssignments.Delete'));

  protected readonly inviteModalOpen = signal(false);
  protected readonly isInviting = signal(false);

  protected readonly revokeTargetId = signal<string | null>(null);

  protected readonly inviteForm = new FormGroup({
    email: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.email] }),
    role: new FormControl(EmployeeRole.BranchManager, { nonNullable: true, validators: [Validators.required] }),
    branchId: new FormControl('', { nonNullable: true }),
  });

  ngOnInit(): void {
    this.loadBranches();
    this.load();
  }

  protected initials(email: string | null | undefined): string {
    return (email ?? '?').slice(0, 2).toUpperCase();
  }

  /** Only meaningful for a real (non-null) `branchId` — the template handles the null "all branches"
   *  case itself (a real, documented domain state, not a missing value; see this file's own comment). */
  protected branchName(branchId: string): string {
    return this.branchNameById().get(branchId) ?? branchId;
  }

  protected roleLabelKey(role: EmployeeRole | undefined): string {
    switch (role) {
      case EmployeeRole.Owner:
        return '::BusinessPanel:Employees:RoleOwner';
      case EmployeeRole.BranchManager:
        return '::BusinessPanel:Employees:RoleBranchManager';
      case EmployeeRole.Cashier:
        return '::BusinessPanel:Employees:RoleCashier';
      default:
        return '::BusinessPanel:Employees:RoleMarketingManager';
    }
  }

  protected goToPage(index: number): void {
    if (index < 0 || index >= this.totalPages()) return;
    this.pageIndex.set(index);
    this.load();
  }

  protected retry(): void {
    this.load();
  }

  protected openInviteModal(): void {
    this.inviteForm.reset({ email: '', role: EmployeeRole.BranchManager, branchId: '' });
    this.inviteModalOpen.set(true);
  }

  protected closeInviteModal(): void {
    this.inviteModalOpen.set(false);
  }

  protected submitInviteForm(): void {
    if (this.inviteForm.invalid) {
      this.inviteForm.markAllAsTouched();
      return;
    }

    const value = this.inviteForm.getRawValue();
    this.isInviting.set(true);
    this.employeesService
      .invite({ email: value.email, role: value.role, branchId: value.branchId || null })
      .subscribe({
        next: () => {
          this.isInviting.set(false);
          this.inviteModalOpen.set(false);
          this.toaster.success('::BusinessPanel:Employees:InviteSentMessage');
          this.pageIndex.set(0);
          this.load();
        },
        error: () => {
          this.isInviting.set(false);
          this.toaster.error('::BusinessPanel:Employees:InviteErrorMessage');
        },
      });
  }

  protected revoke(employee: EmployeeAssignmentDto): void {
    if (!employee.id) return;
    this.confirmation
      .warn('::BusinessPanel:Employees:RevokeConfirmMessage', '::BusinessPanel:Employees:RevokeConfirmTitle')
      .subscribe((status) => {
        if (status !== Confirmation.Status.confirm || !employee.id) return;
        const id = employee.id;
        this.revokeTargetId.set(id);
        this.employeesService.remove(id).subscribe({
          next: () => {
            this.revokeTargetId.set(null);
            this.toaster.success('::BusinessPanel:Employees:RevokedMessage');
            this.load();
          },
          error: () => {
            this.revokeTargetId.set(null);
            this.toaster.error('::BusinessPanel:Employees:RevokeErrorMessage');
          },
        });
      });
  }

  private load(): void {
    this.isLoading.set(true);
    this.loadFailed.set(false);
    this.employeesService
      .getList({ sorting: 'creationTime asc', skipCount: this.pageIndex() * this.pageSize, maxResultCount: this.pageSize })
      .subscribe({
        next: (result) => {
          this.employees.set(result.items ?? []);
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
    this.branchesService.getList({ sorting: 'name asc', skipCount: 0, maxResultCount: 500 }).subscribe({
      next: (result) => {
        const items = result.items ?? [];
        this.branches.set(items);
        const map = new Map<string, string>();
        for (const branch of items) {
          if (branch.id) map.set(branch.id, branch.name ?? branch.id);
        }
        this.branchNameById.set(map);
      },
      error: () => undefined,
    });
  }
}
