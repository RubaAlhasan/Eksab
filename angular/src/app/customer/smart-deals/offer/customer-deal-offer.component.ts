import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { LocalizationPipe, SessionStateService } from '@abp/ng.core';
import { CustomerSmartOffersService } from '../../../proxy/controllers/customer-smart-offers.service';
import type { CustomerSmartOfferDetailsDto } from '../../../proxy/smart-offers/models';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { SkeletonListComponent } from '../../../shared/components/skeleton-list/skeleton-list.component';
import { formatSmartPrice } from '../../../shared/utils/smart-offer-display.util';

/**
 * One deal, opened from an order's "View deal" link. The browse list only shows deals that are on sale now, so this page
 * reads the deal directly. It stays readable after the sale window has ended. The server allows it only for a customer
 * who ordered this deal, so a stranger's link to the same URL gets the same "not available" message as a missing deal.
 */
@Component({
  selector: 'app-customer-deal-offer',
  templateUrl: './customer-deal-offer.component.html',
  styleUrls: ['./customer-deal-offer.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [LocalizationPipe, RouterLink, ErrorStateComponent, SkeletonListComponent],
})
export class CustomerDealOfferComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly customerSmartOffersService = inject(CustomerSmartOffersService);
  private readonly sessionState = inject(SessionStateService);

  protected readonly language = toSignal(this.sessionState.getLanguage$(), {
    initialValue: this.sessionState.getLanguage(),
  });

  protected readonly offer = signal<CustomerSmartOfferDetailsDto | null>(null);
  protected readonly isLoading = signal(true);
  protected readonly loadFailed = signal(false);

  ngOnInit(): void {
    this.load();
  }

  protected retry(): void {
    this.load();
  }

  protected title(deal: CustomerSmartOfferDetailsDto): string {
    return this.language() === 'ar' ? deal.titleAr || deal.titleEn || '' : deal.titleEn || deal.titleAr || '';
  }

  // Same language rule as the orders list: the deal's own language first, the other only when it is missing.
  protected description(deal: CustomerSmartOfferDetailsDto): string {
    return this.language() === 'ar'
      ? deal.descriptionAr || deal.descriptionEn || ''
      : deal.descriptionEn || deal.descriptionAr || '';
  }

  protected basePrice(deal: CustomerSmartOfferDetailsDto): string {
    return formatSmartPrice(deal.basePrice ?? 0, deal.currency, this.language());
  }

  private load(): void {
    const tenantId = this.route.snapshot.paramMap.get('tenantId');
    const offerId = this.route.snapshot.paramMap.get('offerId');
    if (!tenantId || !offerId) {
      this.loadFailed.set(true);
      this.isLoading.set(false);
      return;
    }

    this.isLoading.set(true);
    this.loadFailed.set(false);
    // skipHandleError: the page shows its own "not available" state, so the global error toast would repeat it.
    this.customerSmartOffersService.getOfferDetails(tenantId, offerId, { skipHandleError: true }).subscribe({
      next: deal => {
        this.offer.set(deal);
        this.isLoading.set(false);
      },
      error: () => {
        this.offer.set(null);
        this.loadFailed.set(true);
        this.isLoading.set(false);
      },
    });
  }
}
