import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormArray, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { LocalizationPipe, PermissionService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { BranchesService } from '../../proxy/controllers/branches.service';
import { BillingService } from '../../proxy/controllers/billing.service';
import type { BranchDto, DayOpeningHoursDto } from '../../proxy/branches/models';
import { DayOfWeek } from '../../proxy/branches/day-of-week.enum';
import type { UsageDto } from '../../proxy/billing/models';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { LoadingSpinnerComponent } from '../../shared/components/loading-spinner/loading-spinner.component';
import { ErrorStateComponent } from '../../shared/components/error-state/error-state.component';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { ModalComponent } from '../../shared/components/modal/modal.component';

// Monday-first display order — the week's natural reading order for most of this app's users,
// independent of DayOfWeek's own Sunday=0 numeric storage order.
const WEEK_DAYS: DayOfWeek[] = [
  DayOfWeek.Monday,
  DayOfWeek.Tuesday,
  DayOfWeek.Wednesday,
  DayOfWeek.Thursday,
  DayOfWeek.Friday,
  DayOfWeek.Saturday,
  DayOfWeek.Sunday,
];

const DAY_LABEL_KEYS: Record<DayOfWeek, string> = {
  [DayOfWeek.Monday]: '::BusinessPanel:Branches:DayMonday',
  [DayOfWeek.Tuesday]: '::BusinessPanel:Branches:DayTuesday',
  [DayOfWeek.Wednesday]: '::BusinessPanel:Branches:DayWednesday',
  [DayOfWeek.Thursday]: '::BusinessPanel:Branches:DayThursday',
  [DayOfWeek.Friday]: '::BusinessPanel:Branches:DayFriday',
  [DayOfWeek.Saturday]: '::BusinessPanel:Branches:DaySaturday',
  [DayOfWeek.Sunday]: '::BusinessPanel:Branches:DaySunday',
};

function buildDayGroup(): FormGroup {
  return new FormGroup({
    isClosed: new FormControl(true, { nonNullable: true }),
    openTime: new FormControl('09:00', { nonNullable: true, validators: [Validators.required] }),
    closeTime: new FormControl('18:00', { nonNullable: true, validators: [Validators.required] }),
  });
}

/**
 * Business Portal > Branches — mirrors prototype/business/branches.html, built against
 * `BranchAppService`'s real full CRUD (`GetListAsync`/`CreateAsync`/`UpdateAsync`, whole controller
 * gated on `Eksabli.Branches.Default`; `Create`/`Edit` children gate the two actions this page
 * exposes).
 *
 * Opening hours is a real structured weekly schedule now (`DayOpeningHoursDto[]`, one HTML
 * `<input type="time">` pair per day with a "Closed" toggle) — previously free text into
 * `Branch.OpeningHoursJson` with no schema. "24:00" (midnight) isn't reachable through a native time
 * input (max "23:59"), so a business that's genuinely open to midnight types "23:59" — a deliberate,
 * negligible rounding rather than a custom time-widget just for that one edge case.
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

  protected readonly weekDays = WEEK_DAYS;
  protected readonly dayLabelKey = (day: DayOfWeek): string => DAY_LABEL_KEYS[day];

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
    hours: new FormArray(WEEK_DAYS.map(() => buildDayGroup())),
  });

  protected dayGroup(index: number): FormGroup {
    return this.form.controls.hours.at(index) as FormGroup;
  }

  /** A short "9:00 AM – 10:00 PM" / "Closed" summary per day, for the read-only branch list. */
  protected hoursSummary(branch: BranchDto, day: DayOfWeek): string {
    const entry = branch.openingHours.find((d) => d.dayOfWeek === day);
    if (!entry || entry.isClosed) return '';
    return `${entry.openTime} – ${entry.closeTime}`;
  }

  ngOnInit(): void {
    this.load();
    this.loadUsage();
  }

  protected retry(): void {
    this.load();
  }

  protected openCreateModal(): void {
    this.editingBranchId = null;
    this.form.reset({ name: '', address: '', phone: '' });
    this.resetHours([]);
    this.formModalOpen.set(true);
  }

  protected openEditModal(branch: BranchDto): void {
    if (!branch.id) return;
    this.editingBranchId = branch.id;
    this.form.reset({
      name: branch.name ?? '',
      address: branch.address ?? '',
      phone: branch.phone ?? '',
    });
    this.resetHours(branch.openingHours);
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
    const openingHours: DayOpeningHoursDto[] = WEEK_DAYS.map((day, index) => {
      const dayValue = value.hours[index];
      return {
        dayOfWeek: day,
        isClosed: dayValue.isClosed,
        openTime: dayValue.isClosed ? null : dayValue.openTime,
        closeTime: dayValue.isClosed ? null : dayValue.closeTime,
      };
    });

    const input = {
      name: value.name,
      address: value.address || null,
      phone: value.phone || null,
      openingHours,
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

  private resetHours(existing: DayOpeningHoursDto[]): void {
    const byDay = new Map(existing.map((d) => [d.dayOfWeek, d]));
    WEEK_DAYS.forEach((day, index) => {
      const entry = byDay.get(day);
      this.dayGroup(index).reset({
        isClosed: entry?.isClosed ?? true,
        openTime: entry?.openTime || '09:00',
        closeTime: entry?.closeTime || '18:00',
      });
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
    // skipHandleError: true is load-bearing, not decoration — @abp/ng.core's RestService reports
    // ANY non-2xx response to the global HttpErrorReporterService itself (the full-page "[403] You
    // are not authorized!" overlay), independently of whatever this call's own `error` callback does.
    // Without it, a BranchManager (correctly lacking Eksabli.Billing.ManageOwn) got that overlay on
    // every visit to this page, even though Branches' own Eksabli.Branches.Default WAS granted and
    // load() below would have succeeded fine — confirmed live.
    this.billingService.getMyUsage({ skipHandleError: true }).subscribe({
      next: (result) => this.usage.set(result),
      // Best-effort — a viewer without Billing.ManageOwn just doesn't see the quota banner.
      error: () => undefined,
    });
  }
}
