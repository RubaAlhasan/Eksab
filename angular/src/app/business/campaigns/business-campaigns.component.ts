import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { LocalizationPipe, PermissionService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { CampaignsService } from '../../proxy/controllers/campaigns.service';
import { ReportsService } from '../../proxy/controllers/reports.service';
import { TiersService } from '../../proxy/controllers/tiers.service';
import type { CampaignDto } from '../../proxy/campaigns/models';
import type { CampaignPerformanceDto } from '../../proxy/reports/models';
import type { TierDto } from '../../proxy/wallets/models';
import { CampaignType } from '../../proxy/campaigns/campaign-type.enum';
import { CampaignStatus } from '../../proxy/campaigns/campaign-status.enum';
import { CampaignTargetRuleSegmentType } from '../../proxy/campaigns/campaign-target-rule-segment-type.enum';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { LoadingSpinnerComponent } from '../../shared/components/loading-spinner/loading-spinner.component';
import { ErrorStateComponent } from '../../shared/components/error-state/error-state.component';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { StatusBadgeComponent, StatusBadgeVariant } from '../../shared/components/status-badge/status-badge.component';
import { ModalComponent } from '../../shared/components/modal/modal.component';

type WizardStep = 1 | 2 | 3;

/**
 * Business Portal > Campaigns — mirrors prototype/business/campaigns.html, built against
 * `CampaignAppService`'s real CRUD + `ActivateAsync`/`PreviewTargetSegmentAsync`, plus
 * `IReportsAppService.GetCampaignPerformanceAsync` for real per-campaign stats. No backend changes
 * needed; every endpoint and its proxy already existed, including the exact JSON schemas below
 * (`CampaignRules`/`CampaignSegmentParameters`, both real C# classes with `Parse()` methods — not
 * guessed).
 *
 * **The wizard's Step 3 is genuinely different from the prototype's own flow, for a real reason**:
 * `PreviewTargetSegmentAsync` takes an existing campaign *id* — there's no "preview a draft segment
 * before saving anything" endpoint. So this wizard actually creates the campaign as `Draft` (a real
 * status) at the end of Step 2, THEN calls the real preview for that new id, THEN Step 3 offers
 * "Activate Now" (`ActivateAsync`) or "Save as Draft" (just close — it's already saved). This is the
 * honest real flow, not the prototype's implied "preview before any commit" sequence.
 *
 * **Step 1 rule fields are conditional on `Type`, using the real `RulesJson` schema**
 * (`CampaignRules`, confirmed by reading `Eksabli.Domain/Campaigns/CampaignRules.cs`'s own doc
 * comment): `DoublePoints` → `Multiplier`; `SpendXGetY` → `SpendThreshold`+`BonusPoints`; `Birthday`
 * → `DaysBefore`+`BonusPoints`; `WinBack`/`Vip`/`NewCustomer` → `BonusPoints`; `Referral` has no real
 * evaluator yet (`CampaignSegmentEvaluator`'s own comment: "defined for schema parity... no evaluator
 * yet") — no rule fields shown, with an honest note instead of pretending it does something.
 *
 * **Step 2 (Target Segment) is skipped entirely for `Birthday` campaigns** — `CampaignSegmentEvaluator
 * .EvaluateAsync` branches specially for `Birthday` and evaluates by date-of-birth using the Step 1
 * `DaysBefore` rule field, ignoring `TargetRules` completely (confirmed by reading that method). Only
 * ONE target rule is captured (not the multi-rule list the DTO technically supports) — matches the
 * prototype's own single-segment dropdown, and keeps this first cut's scope reasonable.
 * Segment param fields use the real `CampaignSegmentParameters` schema: `Tier` → `TierId` (a real
 * `TiersService.getList()` dropdown); `Inactive` → `InactiveDays`; `NewCustomer` → `WithinDays`;
 * `All` → no params.
 *
 * **Per-campaign stats are real**, from `GetCampaignPerformanceAsync` (Sent / Rewarded Members / Bonus
 * Points Awarded) — replacing the prototype's own Sent/Opened/Redeemed, since there's no "opened"
 * read-receipt tracking anywhere and "redeemed" isn't a real per-campaign concept the same way. Only
 * fetched for non-`Draft` campaigns (a draft has sent nothing yet) — one call per visible non-draft
 * campaign, acceptable at the same small scale campaigns are inherently quota-limited to (real
 * `EksabliFeatures.MaxCampaigns` enforcement, confirmed in `CampaignAppService.CreateAsync` — same
 * "throws a real UserFriendlyException at the limit" shape as Branches' `MaxBranches`, but with no
 * dedicated usage-check endpoint like Branches' `GetMyUsageAsync` to show a proactive quota banner —
 * the real error just surfaces via the normal toast if hit).
 *
 * `[MISSING BACKEND CAPABILITY]` / deliberate scope reductions:
 * - No Edit or Delete UI — `UpdateAsync`/`DeleteAsync` exist server-side, but re-deriving the wizard's
 *   full state (type-specific rules + segment params) from an existing campaign's `RulesJson`/
 *   `TargetRules` is meaningfully more work than this first cut; not attempted, same "not attempted
 *   this pass" reasoning as Employees/Points Management/Rewards.
 * - No campaign "description" field — `CampaignDto` has no such property (only bilingual name); the
 *   prototype's own description text is dropped, not fabricated.
 */
@Component({
  selector: 'app-business-campaigns',
  templateUrl: './business-campaigns.component.html',
  styleUrls: ['./business-campaigns.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
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
export class BusinessCampaignsComponent implements OnInit {
  private readonly campaignsService = inject(CampaignsService);
  private readonly reportsService = inject(ReportsService);
  private readonly tiersService = inject(TiersService);
  private readonly toaster = inject(ToasterService);
  private readonly permissionService = inject(PermissionService);

  protected readonly Type = CampaignType;
  protected readonly Status = CampaignStatus;
  protected readonly SegmentType = CampaignTargetRuleSegmentType;

  protected readonly campaigns = signal<CampaignDto[]>([]);
  protected readonly isLoading = signal(true);
  protected readonly loadFailed = signal(false);
  private readonly performanceByCampaignId = signal<Map<string, CampaignPerformanceDto>>(new Map());

  protected readonly tiers = signal<TierDto[]>([]);

  protected readonly canCreate = computed(() => this.permissionService.getGrantedPolicy('Eksabli.Campaigns.Create'));
  protected readonly canActivate = computed(() => this.permissionService.getGrantedPolicy('Eksabli.Campaigns.Activate'));

  protected readonly modalOpen = signal(false);
  protected readonly wizardStep = signal<WizardStep>(1);
  protected readonly isSaving = signal(false);
  protected readonly createdCampaign = signal<CampaignDto | null>(null);
  protected readonly previewCount = signal<number | null>(null);
  protected readonly isActivating = signal(false);

  protected readonly step1Form = new FormGroup({
    nameEn: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(128)] }),
    nameAr: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(128)] }),
    type: new FormControl(CampaignType.DoublePoints, { nonNullable: true, validators: [Validators.required] }),
    startDate: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    endDate: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    multiplier: new FormControl<number | null>(2),
    spendThreshold: new FormControl<number | null>(null),
    bonusPoints: new FormControl<number | null>(null),
    daysBefore: new FormControl<number | null>(3),
  });

  protected readonly step2Form = new FormGroup({
    segmentType: new FormControl(CampaignTargetRuleSegmentType.All, { nonNullable: true }),
    tierId: new FormControl<string | null>(null),
    inactiveDays: new FormControl<number | null>(30),
    withinDays: new FormControl<number | null>(7),
  });

  ngOnInit(): void {
    this.loadTiers();
    this.load();
  }

  protected retry(): void {
    this.load();
  }

  protected performance(campaign: CampaignDto): CampaignPerformanceDto | null {
    return campaign.id ? (this.performanceByCampaignId().get(campaign.id) ?? null) : null;
  }

  protected typeLabelKey(type: CampaignType | undefined): string {
    switch (type) {
      case CampaignType.Birthday:
        return '::BusinessPanel:Campaigns:TypeBirthday';
      case CampaignType.SpendXGetY:
        return '::BusinessPanel:Campaigns:TypeSpendXGetY';
      case CampaignType.WinBack:
        return '::BusinessPanel:Campaigns:TypeWinBack';
      case CampaignType.Vip:
        return '::BusinessPanel:Campaigns:TypeVip';
      case CampaignType.NewCustomer:
        return '::BusinessPanel:Campaigns:TypeNewCustomer';
      case CampaignType.Referral:
        return '::BusinessPanel:Campaigns:TypeReferral';
      default:
        return '::BusinessPanel:Campaigns:TypeDoublePoints';
    }
  }

  protected statusLabelKey(status: CampaignStatus | undefined): string {
    switch (status) {
      case CampaignStatus.Active:
        return '::BusinessPanel:Campaigns:StatusActive';
      case CampaignStatus.Ended:
        return '::BusinessPanel:Campaigns:StatusEnded';
      default:
        return '::BusinessPanel:Campaigns:StatusDraft';
    }
  }

  protected statusVariant(status: CampaignStatus | undefined): StatusBadgeVariant {
    switch (status) {
      case CampaignStatus.Active:
        return 'success';
      case CampaignStatus.Ended:
        return 'info';
      default:
        return 'neutral';
    }
  }

  protected activate(campaign: CampaignDto): void {
    if (!campaign.id) return;
    this.isActivating.set(true);
    this.campaignsService.activate(campaign.id).subscribe({
      next: () => {
        this.isActivating.set(false);
        this.toaster.success('::BusinessPanel:Campaigns:ActivatedMessage');
        this.load();
      },
      error: () => {
        this.isActivating.set(false);
        this.toaster.error('::BusinessPanel:Campaigns:ActivateErrorMessage');
      },
    });
  }

  protected openWizard(): void {
    this.step1Form.reset({
      nameEn: '',
      nameAr: '',
      type: CampaignType.DoublePoints,
      startDate: '',
      endDate: '',
      multiplier: 2,
      spendThreshold: null,
      bonusPoints: null,
      daysBefore: 3,
    });
    this.step2Form.reset({ segmentType: CampaignTargetRuleSegmentType.All, tierId: null, inactiveDays: 30, withinDays: 7 });
    this.createdCampaign.set(null);
    this.previewCount.set(null);
    this.wizardStep.set(1);
    this.modalOpen.set(true);
  }

  protected closeWizard(): void {
    this.modalOpen.set(false);
    if (this.createdCampaign()) this.load(); // a draft may have been created even if the user backs out
  }

  protected backStep(): void {
    if (this.wizardStep() === 2) this.wizardStep.set(1);
  }

  protected nextFromStep1(): void {
    if (this.step1Form.controls.nameEn.invalid || this.step1Form.controls.nameAr.invalid || this.step1Form.controls.startDate.invalid || this.step1Form.controls.endDate.invalid) {
      this.step1Form.markAllAsTouched();
      return;
    }

    if (this.step1Form.getRawValue().type === CampaignType.Birthday) {
      this.createDraftAndPreview();
    } else {
      this.wizardStep.set(2);
    }
  }

  protected submitStep2(): void {
    this.createDraftAndPreview();
  }

  private createDraftAndPreview(): void {
    const s1 = this.step1Form.getRawValue();
    const s2 = this.step2Form.getRawValue();

    const rulesJson = this.buildRulesJson(s1.type, s1);
    const targetRules = s1.type === CampaignType.Birthday ? [] : [
      {
        segmentType: s2.segmentType,
        parametersJson: this.buildSegmentParametersJson(s2.segmentType, s2),
      },
    ];

    this.isSaving.set(true);
    this.campaignsService
      .create({
        nameEn: s1.nameEn,
        nameAr: s1.nameAr,
        type: s1.type,
        rulesJson,
        startDate: new Date(s1.startDate).toISOString(),
        endDate: new Date(s1.endDate).toISOString(),
        targetRules,
      })
      .subscribe({
        next: (campaign) => {
          this.createdCampaign.set(campaign);
          if (!campaign.id) {
            this.isSaving.set(false);
            this.wizardStep.set(3);
            return;
          }
          this.campaignsService.previewTargetSegment(campaign.id).subscribe({
            next: (preview) => {
              this.isSaving.set(false);
              this.previewCount.set(preview.matchedMembershipCount ?? 0);
              this.wizardStep.set(3);
            },
            error: () => {
              this.isSaving.set(false);
              this.wizardStep.set(3); // draft is already saved; preview is just informational
            },
          });
        },
        error: () => {
          this.isSaving.set(false);
          this.toaster.error('::BusinessPanel:Campaigns:CreateErrorMessage');
        },
      });
  }

  protected activateFromWizard(): void {
    const campaign = this.createdCampaign();
    if (!campaign?.id) return;
    this.isActivating.set(true);
    this.campaignsService.activate(campaign.id).subscribe({
      next: () => {
        this.isActivating.set(false);
        this.modalOpen.set(false);
        this.toaster.success('::BusinessPanel:Campaigns:ActivatedMessage');
        this.load();
      },
      error: () => {
        this.isActivating.set(false);
        this.toaster.error('::BusinessPanel:Campaigns:ActivateErrorMessage');
      },
    });
  }

  protected saveAsDraft(): void {
    this.modalOpen.set(false);
    this.load();
  }

  private buildRulesJson(type: CampaignType, form: ReturnType<BusinessCampaignsComponent['step1Form']['getRawValue']>): string | null {
    switch (type) {
      case CampaignType.DoublePoints:
        return JSON.stringify({ multiplier: form.multiplier ?? 2 });
      case CampaignType.SpendXGetY:
        return JSON.stringify({ spendThreshold: form.spendThreshold ?? 0, bonusPoints: form.bonusPoints ?? 0 });
      case CampaignType.Birthday:
        return JSON.stringify({ daysBefore: form.daysBefore ?? 3, bonusPoints: form.bonusPoints ?? 0 });
      case CampaignType.WinBack:
      case CampaignType.Vip:
      case CampaignType.NewCustomer:
        return JSON.stringify({ bonusPoints: form.bonusPoints ?? 0 });
      default:
        return null;
    }
  }

  private buildSegmentParametersJson(
    segmentType: CampaignTargetRuleSegmentType,
    form: ReturnType<BusinessCampaignsComponent['step2Form']['getRawValue']>,
  ): string | null {
    switch (segmentType) {
      case CampaignTargetRuleSegmentType.Tier:
        return form.tierId ? JSON.stringify({ tierId: form.tierId }) : null;
      case CampaignTargetRuleSegmentType.Inactive:
        return JSON.stringify({ inactiveDays: form.inactiveDays ?? 30 });
      case CampaignTargetRuleSegmentType.NewCustomer:
        return JSON.stringify({ withinDays: form.withinDays ?? 7 });
      default:
        return null;
    }
  }

  private load(): void {
    this.isLoading.set(true);
    this.loadFailed.set(false);
    this.campaignsService.getList({ sorting: 'creationTime desc', skipCount: 0, maxResultCount: 50 }).subscribe({
      next: (result) => {
        const items = result.items ?? [];
        this.campaigns.set(items);
        this.isLoading.set(false);
        this.loadPerformance(items.filter((c) => c.status !== CampaignStatus.Draft && c.id));
      },
      error: () => {
        this.isLoading.set(false);
        this.loadFailed.set(true);
      },
    });
  }

  private loadPerformance(nonDraftCampaigns: CampaignDto[]): void {
    for (const campaign of nonDraftCampaigns) {
      if (!campaign.id) continue;
      this.reportsService.getCampaignPerformance(campaign.id).subscribe({
        next: (performance) => {
          const map = new Map(this.performanceByCampaignId());
          map.set(campaign.id!, performance);
          this.performanceByCampaignId.set(map);
        },
        error: () => undefined,
      });
    }
  }

  private loadTiers(): void {
    this.tiersService.getList({ sorting: 'minLifetimePoints asc', skipCount: 0, maxResultCount: 100 }).subscribe({
      next: (result) => this.tiers.set(result.items ?? []),
      error: () => undefined,
    });
  }
}
