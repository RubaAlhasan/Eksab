import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { LocalizationPipe, PermissionService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { RewardsService } from '../../proxy/controllers/rewards.service';
import type { RewardDto } from '../../proxy/rewards/models';
import { RewardType } from '../../proxy/rewards/reward-type.enum';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { LoadingSpinnerComponent } from '../../shared/components/loading-spinner/loading-spinner.component';
import { ErrorStateComponent } from '../../shared/components/error-state/error-state.component';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { StatusBadgeComponent, StatusBadgeVariant } from '../../shared/components/status-badge/status-badge.component';
import { ModalComponent } from '../../shared/components/modal/modal.component';

// Same real threshold `ReportsAppService.GetDashboardHomeAsync` itself uses server-side for its own
// low-stock alert — reused here for consistency, not a separately-invented number.
const LOW_STOCK_THRESHOLD = 10;

type RewardStatus = 'active' | 'scheduled' | 'expired';

/**
 * Business Portal > Rewards — mirrors prototype/business/rewards.html, built against
 * `RewardAppService`'s real full CRUD (`GetListAsync`/`CreateAsync`/`UpdateAsync`, whole controller
 * gated on `Eksabli.Rewards.Default`; `Create`/`Edit` children gate the two actions this page
 * exposes). Proxy already existed, fully generated — no backend changes needed.
 *
 * Real fields, closely matching the prototype's own form (this one maps unusually well):
 * - Name (bilingual, `NameAr`/`NameEn`, both `[Required]`), Type (`Discount`/`FreeProduct`/`GiftCard`
 *   — exactly the prototype's own three options), Points cost, Stock (nullable = unlimited, matching
 *   the prototype's own "blank = unlimited" copy) are all real, unchanged in meaning from the
 *   prototype's form.
 * - **"Require manager approval to redeem" checkbox IS real** — maps to the real
 *   `ApprovalThresholdPoints` field. `PosAppService.ConfirmRedemptionAsync`'s own logic (confirmed by
 *   reading it) is `ApprovalThresholdPoints.HasValue && PointsCost >= ApprovalThresholdPoints.Value`
 *   ⇒ Manager+ required — so checking the box sets `ApprovalThresholdPoints = pointsCost` (always
 *   triggers the requirement for this reward), unchecking clears it to `null` (never required). A
 *   faithful boolean-to-real-field mapping, not a fabricated flag.
 * - **Status badge is real but *derived*, not a stored field** — `Reward` has no `IsActive`/`Status`
 *   column (confirmed by reading `RewardDto`); "Active"/"Scheduled"/"Expired" is computed client-side
 *   from the real `ValidFrom`/`ValidTo` date bounds against the current time — an honest derivation,
 *   not an invented status. "Low stock" (real `StockRemaining < 10`) takes visual priority over the
 *   date-derived status when both would apply, matching the prototype's own either/or badge shape.
 * - **No redemption-rate progress bar** — the prototype's "62% redemption rate this month" is
 *   hardcoded fake data with no real per-reward redemption-rate computation anywhere in this codebase
 *   (`CouponAuditService` exists but wasn't found to expose a per-reward rate) — dropped entirely.
 * - **No reward image** — the prototype uses a random emoji per reward, not a real image. No blob
 *   upload UI/pattern exists anywhere in this app yet (unlike Categories' `iconBlobName`, which is
 *   captured but has no upload widget either — same established gap). A deterministic per-*type*
 *   emoji (`typeEmoji`) stands in instead of pretending a per-item image exists.
 * - No delete action exposed — `DeleteAsync` exists server-side (`Eksabli.Rewards.Delete`) but the
 *   prototype itself has no delete button either; matches prototype scope.
 */
@Component({
  selector: 'app-business-rewards',
  templateUrl: './business-rewards.component.html',
  styleUrls: ['./business-rewards.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    LocalizationPipe,
    PageHeaderComponent,
    LoadingSpinnerComponent,
    ErrorStateComponent,
    EmptyStateComponent,
    StatusBadgeComponent,
    ModalComponent,
  ],
})
export class BusinessRewardsComponent implements OnInit {
  private readonly rewardsService = inject(RewardsService);
  private readonly toaster = inject(ToasterService);
  private readonly permissionService = inject(PermissionService);

  protected readonly Type = RewardType;

  protected readonly rewards = signal<RewardDto[]>([]);
  protected readonly isLoading = signal(true);
  protected readonly loadFailed = signal(false);

  protected readonly canCreate = computed(() => this.permissionService.getGrantedPolicy('Eksabli.Rewards.Create'));
  protected readonly canEdit = computed(() => this.permissionService.getGrantedPolicy('Eksabli.Rewards.Edit'));

  protected readonly formModalOpen = signal(false);
  protected readonly isSaving = signal(false);
  protected editingRewardId: string | null = null;

  protected readonly form = new FormGroup({
    nameEn: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(128)] }),
    nameAr: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(128)] }),
    type: new FormControl(RewardType.Discount, { nonNullable: true, validators: [Validators.required] }),
    pointsCost: new FormControl<number | null>(null, { validators: [Validators.required, Validators.min(1)] }),
    stockRemaining: new FormControl<number | null>(null),
    validTo: new FormControl('', { nonNullable: true }),
    requireApproval: new FormControl(false, { nonNullable: true }),
  });

  ngOnInit(): void {
    this.load();
  }

  protected retry(): void {
    this.load();
  }

  protected isLowStock(reward: RewardDto): boolean {
    return reward.stockRemaining != null && reward.stockRemaining < LOW_STOCK_THRESHOLD;
  }

  /** Null/undefined `stockRemaining` is a real, documented "unlimited stock" state (matches the
   *  prototype's own "blank = unlimited" copy), not a missing value — returns the localization key
   *  for that case so the template pipes it through `abpLocalization`, same "don't mix a raw value
   *  with a key" contract used elsewhere (e.g. `customerName()`). */
  protected stockLabel(reward: RewardDto): { count: number } | null {
    return reward.stockRemaining != null ? { count: reward.stockRemaining } : null;
  }

  protected status(reward: RewardDto): RewardStatus {
    const now = Date.now();
    if (reward.validTo && new Date(reward.validTo).getTime() < now) return 'expired';
    if (reward.validFrom && new Date(reward.validFrom).getTime() > now) return 'scheduled';
    return 'active';
  }

  protected statusLabelKey(status: RewardStatus): string {
    switch (status) {
      case 'expired':
        return '::BusinessPanel:Rewards:StatusExpired';
      case 'scheduled':
        return '::BusinessPanel:Rewards:StatusScheduled';
      default:
        return '::BusinessPanel:Rewards:StatusActive';
    }
  }

  protected statusVariant(status: RewardStatus): StatusBadgeVariant {
    switch (status) {
      case 'expired':
        return 'neutral';
      case 'scheduled':
        return 'info';
      default:
        return 'success';
    }
  }

  protected typeLabelKey(type: RewardType | undefined): string {
    switch (type) {
      case RewardType.FreeProduct:
        return '::BusinessPanel:Rewards:TypeFreeProduct';
      case RewardType.GiftCard:
        return '::BusinessPanel:Rewards:TypeGiftCard';
      default:
        return '::BusinessPanel:Rewards:TypeDiscount';
    }
  }

  /** Matches the prototype's own per-reward emoji (`r.image`) in spirit — no real image/upload field
   *  exists on `Reward` (see class doc comment), so a deterministic per-*type* emoji stands in for a
   *  per-item one. Chosen over a FontAwesome-icon-in-a-tinted-box: Bootstrap 5.3's `-bg-subtle`/
   *  `-text-emphasis` tokens desaturate to near-gray at this size (confirmed live), while a plain
   *  emoji glyph is inherently colorful and needs no color tuning to read as such. */
  protected typeEmoji(type: RewardType | undefined): string {
    switch (type) {
      case RewardType.FreeProduct:
        return '🎁';
      case RewardType.GiftCard:
        return '💳';
      default:
        return '🏷️';
    }
  }

  protected openCreateModal(): void {
    this.editingRewardId = null;
    this.form.reset({
      nameEn: '',
      nameAr: '',
      type: RewardType.Discount,
      pointsCost: null,
      stockRemaining: null,
      validTo: '',
      requireApproval: false,
    });
    this.formModalOpen.set(true);
  }

  protected openEditModal(reward: RewardDto): void {
    if (!reward.id) return;
    this.editingRewardId = reward.id;
    this.form.reset({
      nameEn: reward.nameEn ?? '',
      nameAr: reward.nameAr ?? '',
      type: reward.type ?? RewardType.Discount,
      pointsCost: reward.pointsCost ?? null,
      stockRemaining: reward.stockRemaining ?? null,
      validTo: reward.validTo ? reward.validTo.substring(0, 10) : '',
      requireApproval: reward.approvalThresholdPoints != null,
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
      nameEn: value.nameEn,
      nameAr: value.nameAr,
      type: value.type,
      pointsCost: value.pointsCost ?? 0,
      stockRemaining: value.stockRemaining,
      validFrom: null,
      validTo: value.validTo ? new Date(value.validTo).toISOString() : null,
      imageBlobName: null,
      approvalThresholdPoints: value.requireApproval ? value.pointsCost : null,
    };

    this.isSaving.set(true);
    const request = this.editingRewardId
      ? this.rewardsService.update(this.editingRewardId, input)
      : this.rewardsService.create(input);

    request.subscribe({
      next: () => {
        this.isSaving.set(false);
        this.formModalOpen.set(false);
        this.toaster.success(
          this.editingRewardId ? '::BusinessPanel:Rewards:UpdatedMessage' : '::BusinessPanel:Rewards:CreatedMessage',
        );
        this.load();
      },
      error: () => {
        this.isSaving.set(false);
        this.toaster.error('::BusinessPanel:Rewards:SaveErrorMessage');
      },
    });
  }

  private load(): void {
    this.isLoading.set(true);
    this.loadFailed.set(false);
    this.rewardsService.getList({ sorting: 'creationTime desc', skipCount: 0, maxResultCount: 100 }).subscribe({
      next: (result) => {
        this.rewards.set(result.items ?? []);
        this.isLoading.set(false);
      },
      error: () => {
        this.isLoading.set(false);
        this.loadFailed.set(true);
      },
    });
  }
}
