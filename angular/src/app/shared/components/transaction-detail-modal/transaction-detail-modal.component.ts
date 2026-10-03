import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { DatePipe } from '@angular/common';
import { LocalizationPipe } from '@abp/ng.core';
import type { TransactionListItemDto } from '../../../proxy/reports/models';
import { isCredit, transactionSourceLabelKey, transactionTypeLabelKey } from '../../utils/transaction-display.util';
import { ModalComponent } from '../modal/modal.component';

/**
 * Per-transaction breakdown dialog, shared by My Points and Transaction History — both pages previously
 * carried identical copies of this markup. The parent owns both signals (open + selected row), so this
 * component holds no state of its own.
 */
@Component({
  selector: 'app-transaction-detail-modal',
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './transaction-detail-modal.component.html',
  styleUrls: ['./transaction-detail-modal.component.scss'],
  imports: [DatePipe, LocalizationPipe, ModalComponent],
})
export class TransactionDetailModalComponent {
  readonly open = input.required<boolean>();
  readonly transaction = input<TransactionListItemDto | null>(null);
  readonly closed = output<void>();

  protected readonly typeLabelKey = transactionTypeLabelKey;
  protected readonly sourceLabelKey = transactionSourceLabelKey;
  protected readonly isCredit = isCredit;
}
