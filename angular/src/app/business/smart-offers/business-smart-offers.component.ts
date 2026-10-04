import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { LocalizationPipe, PermissionService, SessionStateService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { SmartOffersService } from '../../proxy/controllers/smart-offers.service';
import type { SmartOfferDto, SmartOfferStageDto } from '../../proxy/smart-offers/models';
import { SmartOfferStatus } from '../../proxy/smart-offers/smart-offer-status.enum';
import { SmartPricingStrategy } from '../../proxy/smart-offers/smart-pricing-strategy.enum';
import { Currency } from '../../proxy/shared/currency.enum';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import { LoadingSpinnerComponent } from '../../shared/components/loading-spinner/loading-spinner.component';
import { ErrorStateComponent } from '../../shared/components/error-state/error-state.component';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { PaginationComponent } from '../../shared/components/pagination/pagination.component';
import { StatusBadgeComponent, StatusBadgeVariant } from '../../shared/components/status-badge/status-badge.component';
import { SmartTimelineComponent, SmartTimelineSegment } from '../../shared/components/smart-timeline/smart-timeline.component';
import { formatClock, formatSmartPrice, minuteOfDayIn } from '../../shared/utils/smart-offer-display.util';
import { parseClock } from '../../shared/utils/smart-offer-validation.util';

// Three cards a row, three rows: one screen of deals per page.
const PAGE_SIZE = 9;

/**
 * Business Portal > Smart Deals. The list is the owner's at-a-glance view: what a customer pays right now, how much is
 * left today, when the price next moves, and the day's schedule. Every one of those numbers comes from the server
 * (SmartOfferDto); this page only lays them out.
 */
@Component({
  selector: 'app-business-smart-offers',
  templateUrl: './business-smart-offers.component.html',
  styleUrls: ['./business-smart-offers.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink,
    LocalizationPipe,
    PageHeaderComponent,
    LoadingSpinnerComponent,
    ErrorStateComponent,
    EmptyStateComponent,
    StatusBadgeComponent,
    SmartTimelineComponent,
    PaginationComponent,
  ],
})
export class BusinessSmartOffersComponent implements OnInit {
  private readonly smartOffersService = inject(SmartOffersService);
  private readonly toaster = inject(ToasterService);
  private readonly permissionService = inject(PermissionService);
  private readonly sessionState = inject(SessionStateService);

  protected readonly Status = SmartOfferStatus;
  protected readonly Strategy = SmartPricingStrategy;

  protected readonly language = toSignal(this.sessionState.getLanguage$(), {
    initialValue: this.sessionState.getLanguage(),
  });

  protected readonly offers = signal<SmartOfferDto[]>([]);
  protected readonly isLoading = signal(true);
  protected readonly loadFailed = signal(false);
  protected readonly busyOfferId = signal<string | null>(null);

  protected readonly canCreate = computed(() => this.permissionService.getGrantedPolicy('Eksabli.SmartOffers.Create'));
  protected readonly canEdit = computed(() => this.permissionService.getGrantedPolicy('Eksabli.SmartOffers.Edit'));

  protected readonly pageIndex = signal(0);
  protected readonly totalCount = signal(0);
  protected readonly totalPages = computed(() => Math.max(1, Math.ceil(this.totalCount() / PAGE_SIZE)));

  ngOnInit(): void {
    this.load();
  }

  protected retry(): void {
    this.load();
  }

  protected goToPage(index: number): void {
    if (index < 0 || index >= this.totalPages()) return;
    this.pageIndex.set(index);
    this.load();
  }

  protected money(amount: number | null | undefined, currency: Currency | undefined): string {
    return amount == null ? '' : formatSmartPrice(amount, currency, this.language());
  }

  protected statusVariant(status: SmartOfferStatus | undefined): StatusBadgeVariant {
    switch (status) {
      case SmartOfferStatus.Live:
        return 'success';
      case SmartOfferStatus.BetweenStages:
      case SmartOfferStatus.Scheduled:
        return 'info';
      case SmartOfferStatus.Paused:
        return 'warning';
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

  /** Whole-percent saving on the base price while a discounted price is live. Shown only when it is a real saving. */
  protected discountPercent(offer: SmartOfferDto): number | null {
    if (offer.currentPrice == null || !offer.basePrice || offer.currentPrice >= offer.basePrice) {
      return null;
    }

    return Math.round(((offer.basePrice - offer.currentPrice) / offer.basePrice) * 100);
  }

  protected stageLabel(stage: SmartOfferStageDto): string {
    return `${stage.startTime}–${stage.endTime}`;
  }

  /** Stages as timeline segments. The live one is highlighted by id, which the server supplies (CurrentStageId). */
  protected segments(offer: SmartOfferDto): SmartTimelineSegment[] {
    return (offer.stages ?? [])
      .map(stage => ({
        startMinute: parseClock(stage.startTime ?? '') ?? 0,
        endMinute: parseClock(stage.endTime ?? '') ?? 0,
        label: `${stage.startTime}–${stage.endTime} · ${this.money(stage.price, stage.currency)}`,
        tone: stage.id === offer.currentStageId ? ('live' as const) : ('default' as const),
      }));
  }

  /** The next price change in the restaurant's own clock (HH:mm), not the owner's browser clock. */
  protected nextChangeLocal(offer: SmartOfferDto): string {
    const minute = offer.nextChangeAtUtc && offer.timeZoneId ? minuteOfDayIn(offer.timeZoneId, new Date(offer.nextChangeAtUtc)) : null;
    return minute === null ? '' : formatClock(minute);
  }

  protected nowMinute(offer: SmartOfferDto): number | null {
    return offer.timeZoneId ? minuteOfDayIn(offer.timeZoneId, new Date()) : null;
  }

  protected isTimeBased(offer: SmartOfferDto): boolean {
    return offer.strategy === SmartPricingStrategy.TimeBased;
  }

  protected toggleEnabled(offer: SmartOfferDto, event: Event): void {
    if (!offer.id || this.busyOfferId()) return;

    const next = (event.target as HTMLInputElement).checked;
    this.busyOfferId.set(offer.id);
    this.smartOffersService.setEnabled(offer.id, { isEnabled: next }).subscribe({
      next: updated => {
        this.offers.update(list => list.map(o => (o.id === updated.id ? updated : o)));
        this.busyOfferId.set(null);
        this.toaster.success(next ? '::SmartDeals:EnabledMessage' : '::SmartDeals:PausedMessage');
      },
      error: () => {
        // Put the switch back to what the server still says.
        this.busyOfferId.set(null);
        (event.target as HTMLInputElement).checked = !next;
      },
    });
  }

  private load(): void {
    this.isLoading.set(true);
    this.loadFailed.set(false);
    this.smartOffersService.getList({ sorting: 'creationTime desc', skipCount: this.pageIndex() * PAGE_SIZE, maxResultCount: PAGE_SIZE }).subscribe({
      next: result => {
        this.offers.set(result.items ?? []);
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
