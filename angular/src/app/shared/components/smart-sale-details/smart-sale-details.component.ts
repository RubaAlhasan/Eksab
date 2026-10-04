import { DatePipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { LocalizationPipe, PermissionService, SessionStateService } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import type { Currency } from '../../../proxy/shared/currency.enum';
import { formatSmartPrice } from '../../utils/smart-offer-display.util';

/**
 * The fields a completed or settled smart-deal sale can show. The counter's order DTO and the Transactions tab's
 * sales DTO both fit this shape, so one details view serves both pages. Callers pass only what they have.
 */
export interface SmartSaleDetailsView {
  smartOfferId?: string | null;
  code?: string | null;
  offerTitleEn?: string | null;
  offerTitleAr?: string | null;
  offerDescriptionEn?: string | null;
  offerDescriptionAr?: string | null;
  quantity?: number;
  unitPrice?: number;
  basePrice?: number;
  totalAmount?: number;
  currency?: Currency;
  serviceDate?: string;
  placedAt?: string;
  completedAt?: string | null;
  rejectionReason?: string | null;
  customerName?: string | null;
  customerPhone?: string | null;
  branchName?: string | null;
  staffEmail?: string | null;
}

/**
 * Read-only body of a sale-details modal. The page owns the modal and the open state; this only lays out the sale.
 * Money is formatted with the same helper as the counter, so an amount reads the same on every screen.
 */
@Component({
  selector: 'app-smart-sale-details',
  templateUrl: './smart-sale-details.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, LocalizationPipe, RouterLink],
})
export class SmartSaleDetailsComponent {
  private readonly sessionState = inject(SessionStateService);
  private readonly permissionService = inject(PermissionService);
  private readonly toaster = inject(ToasterService);

  readonly sale = input.required<SmartSaleDetailsView>();

  protected readonly language = toSignal(this.sessionState.getLanguage$(), {
    initialValue: this.sessionState.getLanguage(),
  });

  // The deal's own editor is behind the Smart Deals permission. A cashier sees the sale without the link.
  protected readonly canOpenOffer = this.permissionService.getGrantedPolicy('Eksabli.SmartOffers');

  protected description(): string {
    const sale = this.sale();
    return this.language() === 'ar'
      ? sale.offerDescriptionAr || sale.offerDescriptionEn || ''
      : sale.offerDescriptionEn || sale.offerDescriptionAr || '';
  }

  // A saving row only makes sense when the customer paid less than the regular price.
  protected readonly hasSaving = computed(() => (this.sale().basePrice ?? 0) > (this.sale().unitPrice ?? 0));

  // The deal ID is the reference support and the audit trail use, so it can be copied whole rather than retyped.
  protected async copyDealId(dealId: string): Promise<void> {
    try {
      await navigator.clipboard.writeText(dealId);
      this.toaster.success('::SmartDeals:SaleDetails:IdCopied');
    } catch {
      this.toaster.error('::SmartDeals:SaleDetails:CopyFailed');
    }
  }

  protected money(amount: number | undefined): string {
    return amount == null ? '' : formatSmartPrice(amount, this.sale().currency, this.language());
  }
}
