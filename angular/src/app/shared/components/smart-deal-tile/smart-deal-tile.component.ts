import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { LocalizationPipe, SessionStateService } from '@abp/ng.core';
import type { CustomerSmartOfferDto } from '../../../proxy/smart-offers/models';
import { formatSmartPrice } from '../../utils/smart-offer-display.util';

/**
 * Compact deal for a horizontal strip (the customer home). It shows the same server-computed facts as the full card,
 * but leaves buying to the business page it links to, so a strip of tiles stays light.
 */
@Component({
  selector: 'app-smart-deal-tile',
  templateUrl: './smart-deal-tile.component.html',
  styleUrls: ['./smart-deal-tile.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, LocalizationPipe],
})
export class SmartDealTileComponent {
  readonly deal = input.required<CustomerSmartOfferDto>();

  private readonly sessionState = inject(SessionStateService);

  protected readonly language = toSignal(this.sessionState.getLanguage$(), {
    initialValue: this.sessionState.getLanguage(),
  });

  protected readonly title = computed(() => {
    const deal = this.deal();
    return this.language() === 'ar' ? deal.titleAr || deal.titleEn : deal.titleEn || deal.titleAr;
  });

  protected money(amount: number | null | undefined): string {
    const deal = this.deal();
    return amount == null ? '' : formatSmartPrice(amount, deal.currency, this.language());
  }
}
