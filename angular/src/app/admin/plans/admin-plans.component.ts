import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { LocalizationPipe, PermissionService } from '@abp/ng.core';
import { ConfirmationService, ToasterService, Confirmation } from '@abp/ng.theme.shared';
import { SubscriptionPlansService } from '../../proxy/controllers/subscription-plans.service';
import type { CreateUpdateSubscriptionPlanDto, SubscriptionPlanDto } from '../../proxy/billing/models';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { LoadingSpinnerComponent } from '../../shared/components/loading-spinner/loading-spinner.component';
import { ErrorStateComponent } from '../../shared/components/error-state/error-state.component';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { PaginationComponent } from '../../shared/components/pagination/pagination.component';
import { StatusBadgeComponent } from '../../shared/components/status-badge/status-badge.component';
import { ModalComponent } from '../../shared/components/modal/modal.component';

/**
 * `FeatureLimitsJson`'s well-known keys, per the doc comment on `SubscriptionPlan.cs`
 * (src/Eksabli.Domain/Billing/SubscriptionPlan.cs) — the backend stores this as an unstructured
 * string with no schema validation, so this mapping is a best-effort client-side editing convenience,
 * not a guaranteed contract. Any *other* keys already present in a plan's existing JSON are preserved
 * on save (parsed, merged, re-serialized), never silently dropped.
 */
const FEATURE_KEYS = {
  maxBranches: 'Eksabli.MaxBranches',
  maxActiveMembers: 'Eksabli.MaxActiveMembers',
  maxCampaigns: 'Eksabli.MaxCampaigns',
  smsNotifications: 'Eksabli.SMSNotifications',
  pushNotifications: 'Eksabli.PushNotifications',
} as const;

interface ParsedFeatureLimits {
  maxBranches: number | null;
  maxActiveMembers: number | null;
  maxCampaigns: number | null;
  smsNotifications: boolean;
  pushNotifications: boolean;
}

function parseFeatureLimits(json: string | undefined): { known: ParsedFeatureLimits; rest: Record<string, unknown> } {
  let raw: Record<string, unknown> = {};
  try {
    raw = json ? (JSON.parse(json) as Record<string, unknown>) : {};
  } catch {
    raw = {};
  }

  const known: ParsedFeatureLimits = {
    maxBranches: typeof raw[FEATURE_KEYS.maxBranches] === 'number' ? (raw[FEATURE_KEYS.maxBranches] as number) : null,
    maxActiveMembers:
      typeof raw[FEATURE_KEYS.maxActiveMembers] === 'number' ? (raw[FEATURE_KEYS.maxActiveMembers] as number) : null,
    maxCampaigns: typeof raw[FEATURE_KEYS.maxCampaigns] === 'number' ? (raw[FEATURE_KEYS.maxCampaigns] as number) : null,
    smsNotifications: !!raw[FEATURE_KEYS.smsNotifications],
    pushNotifications: !!raw[FEATURE_KEYS.pushNotifications],
  };

  // Preserve any keys this form doesn't know about (e.g. added by a future feature) rather than
  // silently dropping them on the next save.
  const rest = { ...raw };
  for (const key of Object.values(FEATURE_KEYS)) {
    delete rest[key];
  }

  return { known, rest };
}

function serializeFeatureLimits(known: ParsedFeatureLimits, rest: Record<string, unknown>): string {
  const merged: Record<string, unknown> = { ...rest };
  if (known.maxBranches !== null) merged[FEATURE_KEYS.maxBranches] = known.maxBranches;
  if (known.maxActiveMembers !== null) merged[FEATURE_KEYS.maxActiveMembers] = known.maxActiveMembers;
  if (known.maxCampaigns !== null) merged[FEATURE_KEYS.maxCampaigns] = known.maxCampaigns;
  merged[FEATURE_KEYS.smsNotifications] = known.smsNotifications;
  merged[FEATURE_KEYS.pushNotifications] = known.pushNotifications;
  return JSON.stringify(merged);
}

/** Renders the same feature-limit summary shown in the table's "Limits" column and reused nowhere
 *  else — small enough that a shared component would be premature (single consumer). */
function summarizeFeatureLimits(json: string | undefined): string[] {
  const { known } = parseFeatureLimits(json);
  const parts: string[] = [];
  if (known.maxBranches !== null) parts.push(`${known.maxBranches} branches`);
  if (known.maxActiveMembers !== null) parts.push(`${known.maxActiveMembers} members`);
  if (known.maxCampaigns !== null) parts.push(`${known.maxCampaigns} campaigns`);
  if (known.smsNotifications) parts.push('SMS');
  if (known.pushNotifications) parts.push('Push');
  return parts;
}

/**
 * Admin Portal > Plans — platform subscription-plan catalog CRUD. Wired directly against the real
 * `SubscriptionPlansController`/`SubscriptionPlanAppService`. No search box (the backend's
 * `GetListAsync` takes plain `PagedAndSortedResultRequestDto`, no filter parameter exists — see the
 * pre-implementation report), no Activate/Deactivate (no status field, no such endpoint — only a real
 * hard Delete). Both are documented gaps, not missing polish.
 */
@Component({
  selector: 'app-admin-plans',
  templateUrl: './admin-plans.component.html',
  styleUrls: ['./admin-plans.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
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
export class AdminPlansComponent implements OnInit {
  private readonly plansService = inject(SubscriptionPlansService);
  private readonly confirmation = inject(ConfirmationService);
  private readonly toaster = inject(ToasterService);
  private readonly permissionService = inject(PermissionService);

  private readonly pageSize = 12;

  protected readonly plans = signal<SubscriptionPlanDto[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly isLoading = signal(true);
  protected readonly loadFailed = signal(false);
  protected readonly pageIndex = signal(0);
  protected readonly totalPages = computed(() => Math.max(1, Math.ceil(this.totalCount() / this.pageSize)));

  // One real permission gates Create/Update/Delete alike — three signals kept (per the requested
  // canCreate/canEdit/canDelete shape) rather than one, so the template stays consistent with every
  // other Admin page, even though today they all resolve the same underlying grant.
  protected readonly canCreate = computed(() => this.permissionService.getGrantedPolicy('Eksabli.Billing.ManagePlatform'));
  protected readonly canEdit = computed(() => this.permissionService.getGrantedPolicy('Eksabli.Billing.ManagePlatform'));
  protected readonly canDelete = computed(() => this.permissionService.getGrantedPolicy('Eksabli.Billing.ManagePlatform'));

  protected readonly modalOpen = signal(false);
  protected readonly modalTitleKey = signal('');
  protected readonly isSaving = signal(false);
  private editingPlanId: string | null = null;
  private featureLimitsRest: Record<string, unknown> = {};

  protected readonly form = new FormGroup({
    name: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(64)] }),
    monthlyPrice: new FormControl<number | null>(0, { validators: [Validators.required, Validators.min(0)] }),
    isTrialDefault: new FormControl(false, { nonNullable: true }),
    maxBranches: new FormControl<number | null>(null, { validators: [Validators.min(0)] }),
    maxActiveMembers: new FormControl<number | null>(null, { validators: [Validators.min(0)] }),
    maxCampaigns: new FormControl<number | null>(null, { validators: [Validators.min(0)] }),
    smsNotifications: new FormControl(false, { nonNullable: true }),
    pushNotifications: new FormControl(false, { nonNullable: true }),
  });

  ngOnInit(): void {
    this.load();
  }

  protected readonly summarizeFeatureLimits = summarizeFeatureLimits;

  protected goToPage(index: number): void {
    if (index < 0 || index >= this.totalPages()) return;
    this.pageIndex.set(index);
    this.load();
  }

  protected retry(): void {
    this.load();
  }

  protected openCreateModal(): void {
    this.editingPlanId = null;
    this.featureLimitsRest = {};
    this.modalTitleKey.set('::AdminPanel:Plans:NewTitle');
    this.form.reset({
      name: '',
      monthlyPrice: 0,
      isTrialDefault: false,
      maxBranches: null,
      maxActiveMembers: null,
      maxCampaigns: null,
      smsNotifications: false,
      pushNotifications: false,
    });
    this.modalOpen.set(true);
  }

  protected openEditModal(plan: SubscriptionPlanDto): void {
    this.editingPlanId = plan.id ?? null;
    const { known, rest } = parseFeatureLimits(plan.featureLimitsJson);
    this.featureLimitsRest = rest;
    this.modalTitleKey.set('::AdminPanel:Plans:EditTitle');
    this.form.reset({
      name: plan.name ?? '',
      monthlyPrice: plan.monthlyPrice ?? 0,
      isTrialDefault: plan.isTrialDefault ?? false,
      maxBranches: known.maxBranches,
      maxActiveMembers: known.maxActiveMembers,
      maxCampaigns: known.maxCampaigns,
      smsNotifications: known.smsNotifications,
      pushNotifications: known.pushNotifications,
    });
    this.modalOpen.set(true);
  }

  protected closeModal(): void {
    this.modalOpen.set(false);
  }

  protected submitForm(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const value = this.form.getRawValue();
    const featureLimitsJson = serializeFeatureLimits(
      {
        maxBranches: value.maxBranches,
        maxActiveMembers: value.maxActiveMembers,
        maxCampaigns: value.maxCampaigns,
        smsNotifications: value.smsNotifications,
        pushNotifications: value.pushNotifications,
      },
      this.featureLimitsRest,
    );

    const payload: CreateUpdateSubscriptionPlanDto = {
      name: value.name,
      monthlyPrice: value.monthlyPrice ?? 0,
      featureLimitsJson,
      isTrialDefault: value.isTrialDefault,
    };

    this.isSaving.set(true);
    const request = this.editingPlanId
      ? this.plansService.update(this.editingPlanId, payload)
      : this.plansService.create(payload);

    request.subscribe({
      next: () => {
        this.isSaving.set(false);
        this.modalOpen.set(false);
        this.toaster.success('::AdminPanel:Plans:SavedMessage');
        this.load();
      },
      error: () => {
        this.isSaving.set(false);
        this.toaster.error('::AdminPanel:Plans:SaveErrorMessage');
      },
    });
  }

  protected deletePlan(plan: SubscriptionPlanDto): void {
    if (!plan.id) return;
    this.confirmation
      .warn('::AdminPanel:Plans:DeleteConfirmMessage', '::AdminPanel:Plans:DeleteConfirmTitle')
      .subscribe((status) => {
        if (status !== Confirmation.Status.confirm || !plan.id) return;
        this.plansService.delete(plan.id).subscribe({
          next: () => {
            this.toaster.success('::AdminPanel:Plans:DeletedMessage');
            this.load();
          },
          error: () => this.toaster.error('::AdminPanel:Plans:DeleteErrorMessage'),
        });
      });
  }

  private load(): void {
    this.isLoading.set(true);
    this.loadFailed.set(false);

    this.plansService
      .getList({
        sorting: 'name asc',
        skipCount: this.pageIndex() * this.pageSize,
        maxResultCount: this.pageSize,
      })
      .subscribe({
        next: (result) => {
          this.plans.set(result.items ?? []);
          this.totalCount.set(result.totalCount ?? 0);
          this.isLoading.set(false);
        },
        error: () => {
          this.isLoading.set(false);
          this.loadFailed.set(true);
        },
      });
  }
}
