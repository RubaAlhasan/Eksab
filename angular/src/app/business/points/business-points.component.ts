import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { LocalizationPipe, PermissionService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
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
 * itself, humanized) with the real points-per-unit value. No "Add Rule" UI — the prototype's own
 * button is a stub (`Eksabli.showToast('This would open the rule builder form')`, not a built form
 * even in the mockup), so building a real one here would be new scope beyond parity, not attempted
 * this pass despite `PointRuleAppService.CreateAsync` existing server-side.
 *
 * **Tiers tab** — real via `TierService.getList()`, read-only (matches the prototype's own tab, which
 * is also read-only). No Add/Edit UI despite `TierAppService`'s real full CRUD, same "not attempted
 * this pass" reasoning as Point Rules.
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
  imports: [ReactiveFormsModule, LocalizationPipe, PageHeaderComponent, LoadingSpinnerComponent, ErrorStateComponent, QrScannerComponent],
})
export class BusinessPointsComponent implements OnInit {
  private readonly posService = inject(PosService);
  private readonly pointRulesService = inject(PointRulesService);
  private readonly tiersService = inject(TiersService);
  private readonly toaster = inject(ToasterService);
  private readonly permissionService = inject(PermissionService);

  protected readonly RuleType = PointRuleType;
  protected readonly activeTab = signal<PointsTab>('award');

  protected readonly canViewRules = computed(() => this.permissionService.getGrantedPolicy('Eksabli.PointRules'));
  protected readonly canViewTiers = computed(() => this.permissionService.getGrantedPolicy('Eksabli.Tiers'));

  // --- Award tab ---
  protected readonly identifyMode = signal<IdentifyMode>('qr');
  protected readonly phoneForm = new FormGroup({
    phoneNumber: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
  });
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

  // --- Tiers tab ---
  protected readonly tiers = signal<TierDto[]>([]);
  protected readonly tiersLoading = signal(false);
  protected readonly tiersFailed = signal(false);
  private tiersLoaded = false;

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

  protected lookup(): void {
    if (this.phoneForm.invalid) {
      this.phoneForm.markAllAsTouched();
      return;
    }

    this.isLookingUp.set(true);
    this.lookupFailed.set(false);
    this.customer.set(null);
    this.lastAward.set(null);

    this.posService.lookupCustomerByPhone({ phoneNumber: this.phoneForm.getRawValue().phoneNumber }).subscribe({
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
