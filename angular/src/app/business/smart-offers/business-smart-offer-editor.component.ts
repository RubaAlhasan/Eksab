import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormArray, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { LocalizationPipe, SessionStateService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { startWith } from 'rxjs';
import { SmartOffersService } from '../../proxy/controllers/smart-offers.service';
import type { CreateUpdateSmartOfferDto, CreateUpdateSmartOfferStageDto, SmartOfferDto } from '../../proxy/smart-offers/models';
import { SmartOfferStatus } from '../../proxy/smart-offers/smart-offer-status.enum';
import { SmartPricingStrategy } from '../../proxy/smart-offers/smart-pricing-strategy.enum';
import { Currency } from '../../proxy/shared/currency.enum';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { LoadingSpinnerComponent } from '../../shared/components/loading-spinner/loading-spinner.component';
import { ErrorStateComponent } from '../../shared/components/error-state/error-state.component';
import { StatusBadgeComponent, StatusBadgeVariant } from '../../shared/components/status-badge/status-badge.component';
import { SmartTimelineComponent, SmartTimelineSegment } from '../../shared/components/smart-timeline/smart-timeline.component';
import { browserTimeZone, formatClock, formatSmartPrice } from '../../shared/utils/smart-offer-display.util';
import { StageDraft, StageIssues, parseClock, validateStageDrafts } from '../../shared/utils/smart-offer-validation.util';

// Mirrors SmartOfferConsts.MaxStagesPerOffer only to stop the "add stage" button at the limit. The server still enforces it.
const MAX_STAGES = 12;

// Default window for a new stage: two hours, starting where the previous stage ended.
const DEFAULT_STAGE_MINUTES = 120;

type StageForm = {
  id: FormControl<string | null>;
  startTime: FormControl<string>;
  endTime: FormControl<string>;
  price: FormControl<number | null>;
  quantityLimit: FormControl<number | null>;
};

/**
 * Business Portal > Smart Deals > editor. Configures one offer: fixed price or a day's stages, the time zone and dates
 * it runs in, and the stock per stage. It checks what it can while the owner types (overlaps, order, price bounds) so
 * a save is rarely refused, but the server remains the authority and its message is what shows if it does refuse.
 */
@Component({
  selector: 'app-business-smart-offer-editor',
  templateUrl: './business-smart-offer-editor.component.html',
  styleUrls: ['./business-smart-offer-editor.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    LocalizationPipe,
    PageHeaderComponent,
    LoadingSpinnerComponent,
    ErrorStateComponent,
    StatusBadgeComponent,
    SmartTimelineComponent,
  ],
})
export class BusinessSmartOfferEditorComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly smartOffersService = inject(SmartOffersService);
  private readonly toaster = inject(ToasterService);
  private readonly sessionState = inject(SessionStateService);

  protected readonly Strategy = SmartPricingStrategy;
  protected readonly Currency = Currency;
  protected readonly Status = SmartOfferStatus;
  protected readonly maxStages = MAX_STAGES;

  protected readonly language = toSignal(this.sessionState.getLanguage$(), {
    initialValue: this.sessionState.getLanguage(),
  });

  protected readonly offerId = signal<string | null>(null);
  protected readonly isEditing = computed(() => this.offerId() !== null);

  // Set by the route that opens a deal from a sale. The same page shows the deal, with nothing that can change it.
  protected readonly isReadOnly = signal(false);
  protected readonly titleKey = computed(() =>
    this.isReadOnly() ? '::SmartDeals:ViewTitle' : this.isEditing() ? '::SmartDeals:EditTitle' : '::SmartDeals:NewTitle',
  );
  protected readonly isLoading = signal(false);
  protected readonly loadFailed = signal(false);
  protected readonly isSaving = signal(false);

  /** The last state the server reported for this offer. Null for a brand-new offer. */
  protected readonly liveOffer = signal<SmartOfferDto | null>(null);

  protected readonly form = new FormGroup({
    titleEn: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(128)] }),
    titleAr: new FormControl('', { nonNullable: true, validators: [Validators.required, Validators.maxLength(128)] }),
    descriptionEn: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(1000)] }),
    descriptionAr: new FormControl('', { nonNullable: true, validators: [Validators.maxLength(1000)] }),
    strategy: new FormControl<SmartPricingStrategy>(SmartPricingStrategy.TimeBased, { nonNullable: true }),
    currency: new FormControl<Currency>(Currency.Usd, { nonNullable: true }),
    basePrice: new FormControl<number | null>(null, { validators: [Validators.required, Validators.min(0.01)] }),
    minimumPrice: new FormControl<number | null>(null, { validators: [Validators.min(0.01)] }),
    dailyQuantity: new FormControl<number | null>(null, { validators: [Validators.min(1)] }),
    timeZoneId: new FormControl(browserTimeZone(), { nonNullable: true, validators: [Validators.required] }),
    validFrom: new FormControl('', { nonNullable: true }),
    validTo: new FormControl('', { nonNullable: true }),
    isEnabled: new FormControl(true, { nonNullable: true }),
    stages: new FormArray<FormGroup<StageForm>>([]),
  });

  // Recomputed whenever the form changes. A reactive form is not a signal, so its valueChanges feed one.
  private readonly formChanged = toSignal(this.form.valueChanges.pipe(startWith(null)), { initialValue: null });

  protected get stages(): FormArray<FormGroup<StageForm>> {
    return this.form.controls.stages;
  }

  protected readonly isTimeBased = computed(() => {
    this.formChanged();
    return this.form.controls.strategy.value === SmartPricingStrategy.TimeBased;
  });

  protected readonly issues = computed<StageIssues>(() => {
    this.formChanged();
    const raw = this.form.getRawValue();
    if (raw.strategy !== SmartPricingStrategy.TimeBased) {
      return { perStage: [], overall: null };
    }

    return validateStageDrafts(this.toDrafts(), raw.basePrice, raw.minimumPrice);
  });

  protected readonly previewSegments = computed<SmartTimelineSegment[]>(() => {
    this.formChanged();
    const raw = this.form.getRawValue();
    const issues = this.issues();

    return raw.stages
      .map((stage, index): SmartTimelineSegment | null => {
        const startMinute = parseClock(stage.startTime);
        const endMinute = parseClock(stage.endTime);
        if (startMinute === null || endMinute === null || endMinute <= startMinute) {
          return null;
        }

        return {
          startMinute,
          endMinute,
          label: `${stage.startTime}–${stage.endTime} · ${this.money(stage.price, raw.currency)}`,
          tone: issues.perStage[index] ? ('invalid' as const) : ('default' as const),
        };
      })
      .filter((segment): segment is SmartTimelineSegment => segment !== null);
  });

  protected readonly canSave = computed(() => {
    this.formChanged();
    const issues = this.issues();
    const structurallyValid = this.form.valid && (!this.isTimeBased() || this.stages.length > 0);
    return structurallyValid && !issues.overall && issues.perStage.every(issue => issue === null);
  });

  ngOnInit(): void {
    this.isReadOnly.set(this.route.snapshot.data['readOnly'] === true);
    this.route.paramMap.subscribe(params => {
      const id = params.get('id');
      if (!id || id === 'new') {
        this.startNew();
      } else {
        this.load(id);
      }
    });
  }

  protected retry(): void {
    const id = this.offerId();
    if (id) this.load(id);
    else this.startNew();
  }

  protected money(amount: number | null | undefined, currency: Currency): string {
    return amount == null ? '' : formatSmartPrice(amount, currency, this.language());
  }

  protected statusVariant(status: SmartOfferStatus | undefined): StatusBadgeVariant {
    switch (status) {
      case SmartOfferStatus.Live:
        return 'success';
      case SmartOfferStatus.Paused:
        return 'warning';
      case SmartOfferStatus.BetweenStages:
      case SmartOfferStatus.Scheduled:
        return 'info';
      default:
        return 'neutral';
    }
  }

  protected statusLabelKey(status: SmartOfferStatus | undefined): string {
    switch (status) {
      case SmartOfferStatus.Live:
        return '::SmartDeals:Status:Live';
      case SmartOfferStatus.BetweenStages:
        return '::SmartDeals:Status:BetweenStages';
      case SmartOfferStatus.Scheduled:
        return '::SmartDeals:Status:Scheduled';
      case SmartOfferStatus.Paused:
        return '::SmartDeals:Status:Paused';
      default:
        return '::SmartDeals:Status:Expired';
    }
  }

  protected setStrategy(strategy: SmartPricingStrategy): void {
    if (this.isReadOnly()) return;
    this.form.controls.strategy.setValue(strategy);
    if (strategy === SmartPricingStrategy.TimeBased && this.stages.length === 0) {
      this.addStage();
    }
  }

  protected addStage(): void {
    if (this.isReadOnly() || this.stages.length >= MAX_STAGES) return;

    const last = this.stages.at(this.stages.length - 1)?.getRawValue();
    const start = last ? parseClock(last.endTime) ?? 9 * 60 : 9 * 60;
    const end = Math.min(start + DEFAULT_STAGE_MINUTES, 24 * 60);

    this.stages.push(this.newStage({
      startTime: formatClock(start >= 24 * 60 ? 0 : start),
      endTime: formatClock(end),
      price: last?.price ?? this.form.controls.basePrice.value,
      quantityLimit: null,
    }));
  }

  protected removeStage(index: number): void {
    if (this.isReadOnly()) return;
    this.stages.removeAt(index);
  }

  protected stageError(index: number): string | null {
    return this.issues().perStage[index] ?? null;
  }

  protected save(): void {
    if (this.isReadOnly() || !this.canSave()) {
      this.form.markAllAsTouched();
      return;
    }

    const input = this.toDto();
    this.isSaving.set(true);

    const id = this.offerId();
    const request = id ? this.smartOffersService.update(id, input) : this.smartOffersService.create(input);

    request.subscribe({
      next: () => {
        this.isSaving.set(false);
        this.toaster.success(id ? '::SmartDeals:UpdatedMessage' : '::SmartDeals:CreatedMessage');
        this.router.navigate(['/business/smart-offers']);
      },
      // The interceptor already shows the server's own message, which is the authoritative reason for a refusal.
      error: () => this.isSaving.set(false),
    });
  }

  private startNew(): void {
    this.offerId.set(null);
    this.liveOffer.set(null);
    this.loadFailed.set(false);
    this.form.reset({
      titleEn: '',
      titleAr: '',
      descriptionEn: '',
      descriptionAr: '',
      strategy: SmartPricingStrategy.TimeBased,
      currency: Currency.Usd,
      basePrice: 10,
      minimumPrice: null,
      dailyQuantity: null,
      timeZoneId: browserTimeZone(),
      validFrom: '',
      validTo: '',
      isEnabled: true,
    });
    this.stages.clear();
    this.stages.push(this.newStage({ startTime: '09:00', endTime: '11:00', price: 10, quantityLimit: null }));
  }

  private load(id: string): void {
    this.offerId.set(id);
    this.isLoading.set(true);
    this.loadFailed.set(false);

    this.smartOffersService.get(id).subscribe({
      next: dto => {
        this.liveOffer.set(dto);
        this.patchFrom(dto);
        // Disabled after patching: reset() re-enables every control.
        if (this.isReadOnly()) this.form.disable();
        this.isLoading.set(false);
      },
      error: () => {
        this.isLoading.set(false);
        this.loadFailed.set(true);
      },
    });
  }

  private patchFrom(dto: SmartOfferDto): void {
    this.form.reset({
      titleEn: dto.titleEn ?? '',
      titleAr: dto.titleAr ?? '',
      descriptionEn: dto.descriptionEn ?? '',
      descriptionAr: dto.descriptionAr ?? '',
      strategy: dto.strategy ?? SmartPricingStrategy.TimeBased,
      currency: dto.currency ?? Currency.Usd,
      basePrice: dto.basePrice ?? null,
      minimumPrice: dto.minimumPrice ?? null,
      dailyQuantity: dto.dailyQuantity ?? null,
      timeZoneId: dto.timeZoneId ?? browserTimeZone(),
      validFrom: dto.validFrom ?? '',
      validTo: dto.validTo ?? '',
      isEnabled: dto.isEnabled ?? true,
    });

    this.stages.clear();
    for (const stage of dto.stages ?? []) {
      this.stages.push(this.newStage({
        id: stage.id ?? null,
        startTime: stage.startTime ?? '',
        endTime: stage.endTime ?? '',
        price: stage.price ?? null,
        quantityLimit: stage.quantityLimit ?? null,
      }));
    }
  }

  private newStage(value: {
    id?: string | null;
    startTime: string;
    endTime: string;
    price: number | null;
    quantityLimit: number | null;
  }): FormGroup<StageForm> {
    return new FormGroup<StageForm>({
      id: new FormControl<string | null>(value.id ?? null),
      startTime: new FormControl(value.startTime, { nonNullable: true, validators: [Validators.required] }),
      endTime: new FormControl(value.endTime, { nonNullable: true, validators: [Validators.required] }),
      price: new FormControl<number | null>(value.price, { validators: [Validators.required, Validators.min(0.01)] }),
      quantityLimit: new FormControl<number | null>(value.quantityLimit, { validators: [Validators.min(1)] }),
    });
  }

  private toDrafts(): StageDraft[] {
    return this.form.getRawValue().stages.map(stage => ({
      startTime: stage.startTime,
      endTime: stage.endTime,
      price: stage.price,
      quantityLimit: stage.quantityLimit,
    }));
  }

  private toDto(): CreateUpdateSmartOfferDto {
    const raw = this.form.getRawValue();
    const isTimeBased = raw.strategy === SmartPricingStrategy.TimeBased;

    const stages: CreateUpdateSmartOfferStageDto[] = isTimeBased
      ? raw.stages.map(stage => ({
          id: stage.id,
          startTime: stage.startTime,
          endTime: stage.endTime,
          price: stage.price ?? 0,
          currency: raw.currency,
          quantityLimit: stage.quantityLimit,
        }))
      : [];

    return {
      titleAr: raw.titleAr,
      titleEn: raw.titleEn,
      descriptionAr: raw.descriptionAr || null,
      descriptionEn: raw.descriptionEn || null,
      strategy: raw.strategy,
      currency: raw.currency,
      basePrice: raw.basePrice ?? 0,
      minimumPrice: raw.minimumPrice,
      dailyQuantity: isTimeBased ? null : raw.dailyQuantity,
      timeZoneId: raw.timeZoneId,
      validFrom: raw.validFrom || null,
      validTo: raw.validTo || null,
      isEnabled: raw.isEnabled,
      stages,
    };
  }
}
