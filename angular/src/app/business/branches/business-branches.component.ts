import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { LocalizationPipe, PermissionService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { BranchesService } from '../../proxy/controllers/branches.service';
import { BillingService } from '../../proxy/controllers/billing.service';
import type { BranchDto } from '../../proxy/branches/models';
import type { UsageDto } from '../../proxy/billing/models';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { LoadingSpinnerComponent } from '../../shared/components/loading-spinner/loading-spinner.component';
import { ErrorStateComponent } from '../../shared/components/error-state/error-state.component';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { ModalComponent } from '../../shared/components/modal/modal.component';

/**
 * Business Portal > Branches — mirrors prototype/business/branches.html, built against
 * `BranchAppService`'s real full CRUD (`GetListAsync`/`CreateAsync`/`UpdateAsync`, whole controller
 * gated on `Eksabli.Branches.Default`; `Create`/`Edit` children gate the two actions this page
 * exposes). Proxy (`proxy/branches/*`, `proxy/controllers/branches.service.ts`) already existed, fully
 * generated — no backend changes needed for the CRUD itself.
 *
 * Real fields only, deliberately different from the prototype's exact card shape:
 * - No "Status" badge (Active/Inactive) — `Branch` has no such field anywhere in the domain (confirmed
 *   by reading the entity); every branch that exists is implicitly active, there's no soft-disable
 *   concept. Not shown, rather than a badge that never varies.
 * - No "Members" count per branch — `Membership` isn't branch-scoped anywhere in the domain (confirmed
 *   while building the Analytics page's own "Redemptions by Branch" widget, which hit the identical
 *   gap) — there's no real "members at this branch" figure to compute.
 * - No "QR Code" check-in button/modal — no branch check-in QR concept exists anywhere in this
 *   codebase (the real `WalletQrToken` flow is a *customer's own wallet* QR for a staff member to scan
 *   at POS, a completely different thing from a *branch's* own printable check-in code). The prototype
 *   generates a literal random pixel grid for this — pure decoration with nothing real behind it.
 * - Opening hours is captured as free text into `Branch.OpeningHoursJson`, a freeform string column
 *   (`[StringLength(2000)]`, no enforced schema — confirmed by reading `BranchAppService`/`Branch.cs`)
 *   — same "freeform blob, not a schema the column itself enforces" treatment as
 *   `BusinessProfile.SocialLinksJson` elsewhere in this app. Stored as whatever text is typed, not
 *   parsed/validated as JSON.
 * - **Plan-quota alert IS real** — `IBillingAppService.GetMyUsageAsync()` (`Eksabli.Billing.ManageOwn`)
 *   returns the real `{ BranchCount, MaxBranches }` pair, computed server-side from the same
 *   `FeatureChecker`/`EksabliFeatures.MaxBranches` check `BranchAppService.CreateAsync` itself already
 *   enforces (that create call throws a real `UserFriendlyException` — "You've reached the branch
 *   limit..." — at the actual limit; not duplicated client-side, just surfaced via the normal error
 *   toast if it happens). Best-effort/hidden-on-failure — a viewer without `Billing.ManageOwn` just
 *   doesn't see the banner, doesn't block the branch grid itself.
 * - No delete action exposed — `DeleteAsync` exists server-side (`Eksabli.Branches.Delete`) but the
 *   prototype itself has no delete button either; matches prototype scope, not a gap this page
 *   introduces.
 * - No real pagination — branches are inherently plan-quota-limited to a handful (the whole point of
 *   the quota alert above), so a single bounded `maxResultCount: 100` load covers any real business
 *   without needing paged UI, same "acceptable at this scale" reasoning used elsewhere for small,
 *   naturally-bounded lists.
 */
@Component({
  selector: 'app-business-branches',
  templateUrl: './business-branches.component.html',
  styleUrls: ['./business-branches.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    LocalizationPipe,
    PageHeaderComponent,
    LoadingSpinnerComponent,
    ErrorStateComponent,
    EmptyStateComponent,
    ModalComponent,
  ],
})
export class BusinessBranchesComponent implements OnInit {
  private readonly branchesService = inject(BranchesService);
  private readonly billingService = inject(BillingService);
  private readonly toaster = inject(ToasterService);
  private readonly permissionService = inject(PermissionService);

  protected readonly branches = signal<BranchDto[]>([]);
  protected readonly isLoading = signal(true);
  protected readonly loadFailed = signal(false);

  protected readonly usage = signal<UsageDto | null>(null);

  protected readonly canCreate = computed(() => this.permissionService.getGrantedPolicy('Eksabli.Branches.Create'));
  protected readonly canEdit = computed(() => this.permissionService.getGrantedPolicy('Eksabli.Branches.Edit'));

  protected readonly formModalOpen = signal(false);
  protected readonly isSaving = signal(false);
  protected editingBranchId: string | null = null;

  protected readonly form = new FormGroup({
    name: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(128)] }),
    address: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(512)] }),
    phone: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(32)] }),
    openingHoursJson: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(2000)] }),
  });

  ngOnInit(): void {
    this.load();
    this.loadUsage();
  }

  protected retry(): void {
    this.load();
  }

  protected openCreateModal(): void {
    this.editingBranchId = null;
    this.form.reset({ name: '', address: '', phone: '', openingHoursJson: '' });
    this.formModalOpen.set(true);
  }

  protected openEditModal(branch: BranchDto): void {
    if (!branch.id) return;
    this.editingBranchId = branch.id;
    this.form.reset({
      name: branch.name ?? '',
      address: branch.address ?? '',
      phone: branch.phone ?? '',
      openingHoursJson: branch.openingHoursJson ?? '',
    });
    this.formModalOpen.set(true);
  }

  protected closeFormModal(): void {
    this.formModalOpen.set(false);
  }

  protected submitForm(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const input = {
      name: value.name,
      address: value.address || null,
      phone: value.phone || null,
      openingHoursJson: value.openingHoursJson || null,
      latitude: null,
      longitude: null,
    };

    this.isSaving.set(true);
    const request = this.editingBranchId
      ? this.branchesService.update(this.editingBranchId, input)
      : this.branchesService.create(input);

    request.subscribe({
      next: () => {
        this.isSaving.set(false);
        this.formModalOpen.set(false);
        this.toaster.success(
          this.editingBranchId ? '::BusinessPanel:Branches:UpdatedMessage' : '::BusinessPanel:Branches:CreatedMessage',
        );
        this.load();
        this.loadUsage();
      },
      error: () => {
        this.isSaving.set(false);
        this.toaster.error('::BusinessPanel:Branches:SaveErrorMessage');
      },
    });
  }

  private load(): void {
    this.isLoading.set(true);
    this.loadFailed.set(false);
    this.branchesService.getList({ sorting: 'name asc', skipCount: 0, maxResultCount: 100 }).subscribe({
      next: (result) => {
        this.branches.set(result.items ?? []);
        this.isLoading.set(false);
      },
      error: () => {
        this.isLoading.set(false);
        this.loadFailed.set(true);
      },
    });
  }

  private loadUsage(): void {
    this.billingService.getMyUsage().subscribe({
      next: (result) => this.usage.set(result),
      // Best-effort — a viewer without Billing.ManageOwn just doesn't see the quota banner.
      error: () => undefined,
    });
  }
}
