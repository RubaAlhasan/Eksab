import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { LocalizationPipe, PermissionService } from '@abp/ng.core';
import { Confirmation, ConfirmationService, ToasterService } from '@abp/ng.theme.shared';
import { PosService } from '../../proxy/controllers/pos.service';
import { PointRulesService } from '../../proxy/controllers/point-rules.service';
import { TiersService } from '../../proxy/controllers/tiers.service';
import type { AwardPointsResultDto, CustomerLookupResultDto } from '../../proxy/pos/models';
import type { PointRuleDto, TierDto } from '../../proxy/wallets/models';
import { PointRuleType } from '../../proxy/wallets/point-rule-type.enum';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { LoadingSpinnerComponent } from '../../shared/components/loading-spinner/loading-spinner.component';
import { ErrorStateComponent } from '../../shared/components/error-state/error-state.component';
import { QrScannerComponent, QrScanErrorReason } from '../../shared/components/qr-scanner/qr-scanner.component';
import { PhoneInputComponent } from '../../shared/components/phone-input/phone-input.component';
import { ModalComponent } from '../../shared/components/modal/modal.component';

type PointsTab = 'award' | 'rules' | 'tiers';
type IdentifyMode = 'qr' | 'phone';

/**
 * Business Portal > Points Management — mirrors prototype/business/points-management.html's three
 * tabs, built against real `PosAppService`/`PointRuleAppService`/`TierAppService`, but with a real,
 * significant scope reduction from the prototype's own shape — documented per-tab below. No backend
 * changes needed anywhere; every endpoint and its proxy already existed.
 *
 * **Route/permission shape is genuinely different from every other page in this app**: `PosController`
 * has no ABP permission at all (`[Authorize]` only — any authenticated user). The real gate is a
 * custom role-hierarchy check inside `PosAppService` itself (`CheckStaffRoleAsync`, reading the
 * caller's own `EmployeeAssignment.Role` — Owner/BranchManager can award, Cashier can also award but
 * not manually adjust, etc.) — NOT an `[Authorize(EksabliPermissions...)]` attribute, because invited
 * staff hold zero ABP permission grants today (only the seeded tenant Owner does; confirmed by reading
 * `PosAppService`'s own code comment on `CheckStaffRoleAsync`). So this page's route has **no**
 * `data.requiredPolicy` — the Award tab is offered to any authenticated business-realm visitor, and a
 * role mismatch surfaces as the real backend error (a toast) at award time, exactly matching how the
 * API itself is actually gated. Point Rules / Tiers tabs DO have real ABP permissions
 * (`Eksabli.PointRules.Default` / `Eksabli.Tiers.Default`) and are shown/hidden per-tab accordingly —
 * same "no single umbrella policy" shape as `AdminLayoutComponent`'s `AbpAccount::MyAccount` entry
 * (empty-string "always granted" permission).
 *
 * **Award Points tab** — real, matching the prototype's two identification modes:
 * - **Scan QR** (default, matching the prototype's own default tab): a real, camera-based scan via
 *   `QrScannerComponent` (`jsqr`, no server round-trip until a code is actually found). The decoded
 *   payload is the customer's *own* wallet QR token, minted client-side on their phone
 *   (`GetMyWalletQrTokenAsync`) and shown to this device's camera — this is genuinely "scan a QR opened
 *   on another, nearby phone," not a same-device code. Unlike Phone Lookup, there's no separate
 *   identify-then-award step: `AwardPointsByQrAsync` burns the single-use token on the very first read
 *   (see its own server-side comment), so scanning a code *is* the award call, using whatever sale
 *   amount is already in the field — there's no safe way to "peek" the token first without consuming it.
 * - **Phone Lookup**: real, `PosService.lookupCustomerByPhone` → `PosService.awardPointsByCustomerId`.
 *   Kept as the fallback for low-connectivity/camera-less setups — same QR-preferred/phone-fallback
 *   shape documented for reward redemption in `docs/eksabli-loyalty-platform/07-loyalty-engine.md#9`.
 * - **No live points-calculation preview** — the prototype shows a running "base × tier × campaign"
 *   breakdown as the sale amount is typed. The real calculation (`PosAppService.ComputePointsAsync`)
 *   only runs *inside* the actual award call — there's no separate preview/simulate endpoint, and
 *   reimplementing that pipeline (base rule × tier multiplier × `ICampaignRulesEngine` campaign
 *   multiplier, floor-rounded) client-side would risk silently drifting from the real logic. Instead:
 *   click Award, then show the REAL `AwardPointsResultDto` the backend actually computed (points
 *   awarded, new balance, new tier) — after the fact, not a speculative preview.
 *
 * **Point Rules tab** — real via `PointRuleService.getList()`, but `PointRuleDto` itself is much
 * thinner than the prototype's table implies: only `RuleType` (`PerCurrencyUnit`/`PerVisit`) and
 * `PointsPerUnit` are real fields — there is no rule "label"/name and no Active/Inactive status
 * anywhere on the entity (confirmed by reading `PointRuleDto`/`PointRule`). The prototype's "Rule"
 * name column and status badge are dropped; shown as "Per $1 spent" / "Per visit" (the `RuleType`
 * itself, humanized) with the real points-per-unit value. Create/Edit/Delete are real, against
 * `PointRuleAppService`, matching the Categories-style modal pattern used elsewhere in this app. Two
 * real backend constraints the UI respects rather than fighting:
 * - `CreateAsync` throws if a rule of that `RuleType` already exists (confirmed in
 *   `PointRuleAppService.cs`) — at most one `PerCurrencyUnit` and one `PerVisit` rule per tenant. The
 *   create form's Rule Type options exclude whichever type(s) are already configured.
 * - `UpdateAsync` only ever calls `rule.SetPointsPerUnit(...)` — **`RuleType` cannot be changed on an
 *   existing rule**, even though `CreateUpdatePointRuleDto` has the field (confirmed by reading the
 *   method body — it never touches `RuleType`). The edit form disables that field rather than silently
 *   accepting a value the backend would ignore; delete-and-recreate is the real way to change a rule's
 *   type.
 *
 * **Tiers tab** — real via `TierService.getList()`. Create/Edit/Delete are real, against
 * `TierAppService`, which (unlike Point Rules) genuinely updates all three fields on edit — no
 * analogous "ignored field" caveat here.
 *
 * The rounding-policy note ("fractional points always round down") is real — matches
 * `PosAppService.ComputePointsAsync`'s own `Math.Floor` behavior exactly, not just copied from the
 * prototype's copy.
 */
@Component({
  selector: 'app-business-points',
  templateUrl: './business-points.component.html',
  styleUrls: ['./business-points.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    LocalizationPipe,
    PageHeaderComponent,
    LoadingSpinnerComponent,
    ErrorStateComponent,
    QrScannerComponent,
    PhoneInputComponent,
    ModalComponent,
  ],
})
export class BusinessPointsComponent implements OnInit {
  private readonly posService = inject(PosService);
  private readonly pointRulesService = inject(PointRulesService);
  private readonly tiersService = inject(TiersService);
  private readonly toaster = inject(ToasterService);
  private readonly permissionService = inject(PermissionService);
  private readonly confirmation = inject(ConfirmationService);

  protected readonly RuleType = PointRuleType;
  protected readonly activeTab = signal<PointsTab>('award');

  protected readonly canViewRules = computed(() => this.permissionService.getGrantedPolicy('Eksabli.PointRules'));
  protected readonly canViewTiers = computed(() => this.permissionService.getGrantedPolicy('Eksabli.Tiers'));
  protected readonly canCreateRules = computed(() => this.permissionService.getGrantedPolicy('Eksabli.PointRules.Create'));
  protected readonly canEditRules = computed(() => this.permissionService.getGrantedPolicy('Eksabli.PointRules.Edit'));
  protected readonly canDeleteRules = computed(() => this.permissionService.getGrantedPolicy('Eksabli.PointRules.Delete'));
  protected readonly canCreateTiers = computed(() => this.permissionService.getGrantedPolicy('Eksabli.Tiers.Create'));
  protected readonly canEditTiers = computed(() => this.permissionService.getGrantedPolicy('Eksabli.Tiers.Edit'));
  protected readonly canDeleteTiers = computed(() => this.permissionService.getGrantedPolicy('Eksabli.Tiers.Delete'));

  // --- Award tab ---
  protected readonly identifyMode = signal<IdentifyMode>('qr');
  // Combined "+<countryCallingCode><digits>" value emitted by PhoneInputComponent — see that
  // component's own comment for why this stays one plain string rather than a real FormGroup: it's the
  // exact shape PosService.lookupCustomerByPhone/PhoneNumberNormalizer already expect, no DTO change.
  protected readonly phoneNumberValue = signal('');
  protected readonly saleAmount = new FormControl<number | null>(null, { validators: [Validators.min(0)] });

  protected readonly isLookingUp = signal(false);
  protected readonly lookupFailed = signal(false);
  protected readonly customer = signal<CustomerLookupResultDto | null>(null);
  protected readonly isAwarding = signal(false);
  protected readonly lastAward = signal<AwardPointsResultDto | null>(null);
  protected readonly cameraErrorKey = signal<string | null>(null);

  // --- Rules tab ---
  protected readonly rules = signal<PointRuleDto[]>([]);
  protected readonly rulesLoading = signal(false);
  protected readonly rulesFailed = signal(false);
  private rulesLoaded = false;

  protected readonly ruleModalOpen = signal(false);
  protected readonly ruleModalTitle = signal('');
  protected readonly isSavingRule = signal(false);
  private editingRuleId: string | null = null;

  protected readonly ruleForm = new FormGroup({
    ruleType: new FormControl(PointRuleType.PerCurrencyUnit, { nonNullable: true, validators: [Validators.required] }),
    pointsPerUnit: new FormControl<number | null>(null, { validators: [Validators.required, Validators.min(0)] }),
  });

  // Create-mode options only — a rule of a type that already exists would be rejected server-side
  // (PointRuleAppService.CreateAsync throws if one exists), so don't offer it in the first place.
  protected readonly availableRuleTypesForCreate = computed(() => {
    const usedTypes = new Set(this.rules().map((r) => r.ruleType));
    return [PointRuleType.PerCurrencyUnit, PointRuleType.PerVisit].filter((type) => !usedTypes.has(type));
  });

  // --- Tiers tab ---
  protected readonly tiers = signal<TierDto[]>([]);
  protected readonly tiersLoading = signal(false);
  protected readonly tiersFailed = signal(false);
  private tiersLoaded = false;

  protected readonly tierModalOpen = signal(false);
  protected readonly tierModalTitle = signal('');
  protected readonly isSavingTier = signal(false);
  private editingTierId: string | null = null;

  protected readonly tierForm = new FormGroup({
    name: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(64)] }),
    minLifetimePoints: new FormControl<number | null>(null, { validators: [Validators.required, Validators.min(0)] }),
    multiplier: new FormControl<number | null>(null, { validators: [Validators.required, Validators.min(0)] }),
  });

  ngOnInit(): void {
    if (this.activeTab() === 'rules') this.loadRules();
    if (this.activeTab() === 'tiers') this.loadTiers();
  }

  protected selectTab(tab: PointsTab): void {
    this.activeTab.set(tab);
    if (tab === 'rules' && !this.rulesLoaded) this.loadRules();
    if (tab === 'tiers' && !this.tiersLoaded) this.loadTiers();
  }

  protected customerName(customer: CustomerLookupResultDto): string | null {
    const name = [customer.firstName, customer.lastName].filter(Boolean).join(' ').trim();
    return name || null;
  }

  protected customerInitials(customer: CustomerLookupResultDto): string {
    const name = this.customerName(customer);
    if (!name) return '?';
    return name
      .split(' ')
      .filter(Boolean)
      .map((word) => word[0])
      .join('')
      .slice(0, 2)
      .toUpperCase();
  }

  protected ruleTypeLabelKey(type: PointRuleType | undefined): string {
    return type === PointRuleType.PerVisit ? '::BusinessPanel:Points:RuleTypePerVisit' : '::BusinessPanel:Points:RuleTypePerCurrencyUnit';
  }

  protected setIdentifyMode(mode: IdentifyMode): void {
    if (this.identifyMode() === mode) return;
    this.identifyMode.set(mode);
    // Switching modes mid-transaction would otherwise leave a stale identified-customer card or
    // camera error showing next to the *other* mode's controls.
    this.customer.set(null);
    this.lookupFailed.set(false);
    this.cameraErrorKey.set(null);
  }

  /**
   * Fires once per scanned QR — the token is already single-use server-side (burned on the very
   * first read of `AwardPointsByQrAsync`), so there's no separate "identify" step to run first; this
   * *is* the award call, using whatever sale amount is already in the field.
   */
  protected onQrScanned(qrToken: string): void {
    if (this.isAwarding()) return; // ignore a stray second scan while the first is still in flight

    this.cameraErrorKey.set(null);
    this.isAwarding.set(true);
    this.posService.awardPointsByQr({ qrToken, purchaseAmount: this.saleAmount.value }).subscribe({
      next: (result) => {
        this.isAwarding.set(false);
        this.lastAward.set(result);
        this.toaster.success('::BusinessPanel:Points:AwardSuccessMessage');
      },
      error: () => {
        this.isAwarding.set(false);
        this.toaster.error('::BusinessPanel:Points:QrAwardErrorMessage');
      },
    });
  }

  protected onQrScanError(reason: QrScanErrorReason): void {
    this.cameraErrorKey.set(
      reason === 'permission-denied'
        ? '::BusinessPanel:Points:QrPermissionDenied'
        : reason === 'not-supported'
          ? '::BusinessPanel:Points:QrNotSupported'
          : '::BusinessPanel:Points:QrUnknownError',
    );
  }

  protected onPhoneChange(value: string): void {
    this.phoneNumberValue.set(value);
  }

  // Bound to (submit), not (ngSubmit) — (ngSubmit) only intercepts the native `submit` event (and stops
  // the browser's own default full-page-reload/GET-navigation behavior) via a form directive
  // (NgForm/FormGroupDirective), which requires either FormsModule or a real [formGroup] binding on the
  // <form>. This form has neither (the phone value is a plain signal via app-phone-input, not a real
  // FormGroup) — so with (ngSubmit), clicking the submit button silently fell through to the browser's
  // default form submission, reloading the page before the lookup's HTTP response ever had a chance to
  // render. preventDefault() here is what (ngSubmit) would otherwise have handled for us.
  protected onSubmit(event: Event): void {
    event.preventDefault();
    this.lookup();
  }

  protected lookup(): void {
    if (!this.phoneNumberValue()) {
      return;
    }

    this.isLookingUp.set(true);
    this.lookupFailed.set(false);
    this.customer.set(null);
    this.lastAward.set(null);

    this.posService.lookupCustomerByPhone({ phoneNumber: this.phoneNumberValue() }).subscribe({
      next: (result) => {
        this.customer.set(result);
        this.isLookingUp.set(false);
      },
      error: () => {
        this.isLookingUp.set(false);
        this.lookupFailed.set(true);
      },
    });
  }

  protected award(): void {
    const customer = this.customer();
    if (!customer?.customerId) return;

    this.isAwarding.set(true);
    this.posService
      .awardPointsByCustomerId(customer.customerId, { purchaseAmount: this.saleAmount.value })
      .subscribe({
        next: (result) => {
          this.isAwarding.set(false);
          this.lastAward.set(result);
          // Reflect the real new balance immediately rather than re-fetching.
          this.customer.set({ ...customer, balance: result.newBalance });
          this.toaster.success('::BusinessPanel:Points:AwardSuccessMessage');
        },
        error: () => {
          this.isAwarding.set(false);
          this.toaster.error('::BusinessPanel:Points:AwardErrorMessage');
        },
      });
  }

  protected retryRules(): void {
    this.loadRules();
  }

  protected retryTiers(): void {
    this.loadTiers();
  }

  // --- Rules CRUD ---

  protected openCreateRuleModal(): void {
    this.editingRuleId = null;
    this.ruleModalTitle.set('::BusinessPanel:Points:NewRuleTitle');
    this.ruleForm.reset({ ruleType: this.availableRuleTypesForCreate()[0] ?? PointRuleType.PerCurrencyUnit, pointsPerUnit: null });
    this.ruleForm.controls.ruleType.enable();
    this.ruleModalOpen.set(true);
  }

  protected openEditRuleModal(rule: PointRuleDto): void {
    this.editingRuleId = rule.id ?? null;
    this.ruleModalTitle.set('::BusinessPanel:Points:EditRuleTitle');
    this.ruleForm.reset({ ruleType: rule.ruleType ?? PointRuleType.PerCurrencyUnit, pointsPerUnit: rule.pointsPerUnit ?? null });
    // RuleType can't actually change on an existing rule — PointRuleAppService.UpdateAsync never reads
    // it (see this file's own top comment). Disable it here rather than let someone pick a different
    // type and have the save silently not apply it.
    this.ruleForm.controls.ruleType.disable();
    this.ruleModalOpen.set(true);
  }

  protected closeRuleModal(): void {
    this.ruleModalOpen.set(false);
  }

  protected submitRuleForm(): void {
    if (this.ruleForm.invalid) {
      this.ruleForm.markAllAsTouched();
      return;
    }

    const value = this.ruleForm.getRawValue();
    const payload = { ruleType: value.ruleType, pointsPerUnit: value.pointsPerUnit! };

    this.isSavingRule.set(true);
    const request = this.editingRuleId
      ? this.pointRulesService.update(this.editingRuleId, payload)
      : this.pointRulesService.create(payload);

    request.subscribe({
      next: () => {
        this.isSavingRule.set(false);
        this.ruleModalOpen.set(false);
        this.toaster.success('::BusinessPanel:Points:RuleSavedMessage');
        this.loadRules();
      },
      error: () => {
        this.isSavingRule.set(false);
        // A duplicate-type create (409-shaped UserFriendlyException) surfaces via the global ABP error
        // interceptor's own toast — this generic one covers every other failure path.
        this.toaster.error('::BusinessPanel:Points:RuleSaveErrorMessage');
      },
    });
  }

  protected deleteRule(rule: PointRuleDto): void {
    if (!rule.id) return;
    this.confirmation
      .warn('::BusinessPanel:Points:DeleteRuleConfirmMessage', '::BusinessPanel:Points:DeleteRuleConfirmTitle')
      .subscribe((status) => {
        if (status !== Confirmation.Status.confirm || !rule.id) return;
        this.pointRulesService.delete(rule.id).subscribe({
          next: () => {
            this.toaster.success('::BusinessPanel:Points:RuleDeletedMessage');
            this.loadRules();
          },
          error: () => this.toaster.error('::BusinessPanel:Points:RuleDeleteErrorMessage'),
        });
      });
  }

  // --- Tiers CRUD ---

  protected openCreateTierModal(): void {
    this.editingTierId = null;
    this.tierModalTitle.set('::BusinessPanel:Points:NewTierTitle');
    this.tierForm.reset({ name: '', minLifetimePoints: null, multiplier: null });
    this.tierModalOpen.set(true);
  }

  protected openEditTierModal(tier: TierDto): void {
    this.editingTierId = tier.id ?? null;
    this.tierModalTitle.set('::BusinessPanel:Points:EditTierTitle');
    this.tierForm.reset({
      name: tier.name ?? '',
      minLifetimePoints: tier.minLifetimePoints ?? null,
      multiplier: tier.multiplier ?? null,
    });
    this.tierModalOpen.set(true);
  }

  protected closeTierModal(): void {
    this.tierModalOpen.set(false);
  }

  protected submitTierForm(): void {
    if (this.tierForm.invalid) {
      this.tierForm.markAllAsTouched();
      return;
    }

    const value = this.tierForm.getRawValue();
    const payload = { name: value.name, minLifetimePoints: value.minLifetimePoints!, multiplier: value.multiplier! };

    this.isSavingTier.set(true);
    const request = this.editingTierId
      ? this.tiersService.update(this.editingTierId, payload)
      : this.tiersService.create(payload);

    request.subscribe({
      next: () => {
        this.isSavingTier.set(false);
        this.tierModalOpen.set(false);
        this.toaster.success('::BusinessPanel:Points:TierSavedMessage');
        this.loadTiers();
      },
      error: () => {
        this.isSavingTier.set(false);
        this.toaster.error('::BusinessPanel:Points:TierSaveErrorMessage');
      },
    });
  }

  protected deleteTier(tier: TierDto): void {
    if (!tier.id) return;
    this.confirmation
      .warn('::BusinessPanel:Points:DeleteTierConfirmMessage', '::BusinessPanel:Points:DeleteTierConfirmTitle')
      .subscribe((status) => {
        if (status !== Confirmation.Status.confirm || !tier.id) return;
        this.tiersService.delete(tier.id).subscribe({
          next: () => {
            this.toaster.success('::BusinessPanel:Points:TierDeletedMessage');
            this.loadTiers();
          },
          error: () => this.toaster.error('::BusinessPanel:Points:TierDeleteErrorMessage'),
        });
      });
  }

  private loadRules(): void {
    this.rulesLoading.set(true);
    this.rulesFailed.set(false);
    this.pointRulesService.getList({ skipCount: 0, maxResultCount: 100 }).subscribe({
      next: (result) => {
        this.rules.set(result.items ?? []);
        this.rulesLoading.set(false);
        this.rulesLoaded = true;
      },
      error: () => {
        this.rulesLoading.set(false);
        this.rulesFailed.set(true);
      },
    });
  }

  private loadTiers(): void {
    this.tiersLoading.set(true);
    this.tiersFailed.set(false);
    this.tiersService.getList({ sorting: 'minLifetimePoints asc', skipCount: 0, maxResultCount: 100 }).subscribe({
      next: (result) => {
        this.tiers.set(result.items ?? []);
        this.tiersLoading.set(false);
        this.tiersLoaded = true;
      },
      error: () => {
        this.tiersLoading.set(false);
        this.tiersFailed.set(true);
      },
    });
  }
}
