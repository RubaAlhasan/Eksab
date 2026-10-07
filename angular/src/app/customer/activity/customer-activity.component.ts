import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { forkJoin, of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
import { MembershipsService } from '../../proxy/controllers/memberships.service';
import { WalletService } from '../../proxy/controllers/wallet.service';
import type { PointsWalletDto } from '../../proxy/wallets/models';
import type { TransactionListItemDto } from '../../proxy/reports/models';
import { PointsTransactionType } from '../../proxy/wallets/points-transaction-type.enum';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../shared/components/error-state/error-state.component';
import { SkeletonListComponent } from '../../shared/components/skeleton-list/skeleton-list.component';
import { TransactionDetailModalComponent } from '../../shared/components/transaction-detail-modal/transaction-detail-modal.component';
import { isCredit, transactionTypeLabelKey } from '../../shared/utils/transaction-display.util';

// How many of the customer's businesses to read from, and how many rows to take from each. Bounded so a customer who
// belongs to many businesses does not fan out into dozens of requests for one page.
const WALLET_FANOUT = 10;
const ROWS_PER_WALLET = 25;
const PREVIEW_LIMIT = 50;

interface ActivityRow {
  transaction: TransactionListItemDto;
  businessName: string;
}

/**
 * Every business's points activity in one list, newest first. Each business is read with the same tenant-scoped
 * history call the points page uses, and the results are merged here, the same way Home's recent-activity preview is.
 */
@Component({
  selector: 'app-customer-activity',
  templateUrl: './customer-activity.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink,
    DatePipe,
    LocalizationPipe,
    EmptyStateComponent,
    ErrorStateComponent,
    SkeletonListComponent,
    TransactionDetailModalComponent,
  ],
})
export class CustomerActivityComponent implements OnInit {
  private readonly membershipsService = inject(MembershipsService);
  private readonly walletService = inject(WalletService);

  protected readonly typeLabelKey = transactionTypeLabelKey;
  protected readonly isCredit = isCredit;

  // "All" is null; the rest are the ledger types a customer can meaningfully filter on.
  protected readonly filters: { type: PointsTransactionType | null; labelKey: string }[] = [
    { type: null, labelKey: '::Wallet:Transactions:FilterAll' },
    { type: PointsTransactionType.Earn, labelKey: transactionTypeLabelKey(PointsTransactionType.Earn) },
    { type: PointsTransactionType.Redeem, labelKey: transactionTypeLabelKey(PointsTransactionType.Redeem) },
    { type: PointsTransactionType.Expire, labelKey: transactionTypeLabelKey(PointsTransactionType.Expire) },
  ];

  protected readonly filter = signal<PointsTransactionType | null>(null);
  protected readonly rows = signal<ActivityRow[]>([]);
  protected readonly isLoading = signal(true);
  protected readonly loadFailed = signal(false);
  protected readonly hasWallets = signal(true);

  protected readonly selectedTransaction = signal<TransactionListItemDto | null>(null);
  protected readonly detailsOpen = signal(false);

  protected readonly isEmpty = computed(() => !this.isLoading() && !this.loadFailed() && this.rows().length === 0);

  // "No activity yet" only when that is actually true (no wallets, or no filter narrowing the view). A filter that
  // matches nothing, with history sitting elsewhere, gets the same "nothing matches this filter" title the per-business
  // Transactions page already uses — not a claim that the customer has no activity at all.
  protected readonly emptyTitleKey = computed(() =>
    this.hasWallets() && this.filter() !== null ? '::Wallet:Transactions:FilterEmpty' : '::Wallet:Activity:Empty',
  );
  protected readonly emptyDescriptionKey = computed(() =>
    !this.hasWallets() || this.filter() === null ? '::Wallet:Activity:EmptyHint' : null,
  );

  ngOnInit(): void {
    this.load();
  }

  protected selectFilter(type: PointsTransactionType | null): void {
    if (this.filter() === type) return;
    this.filter.set(type);
    this.load();
  }

  protected retry(): void {
    this.load();
  }

  protected openDetails(transaction: TransactionListItemDto): void {
    this.selectedTransaction.set(transaction);
    this.detailsOpen.set(true);
  }

  private load(): void {
    this.isLoading.set(true);
    this.loadFailed.set(false);

    this.membershipsService.getMyWallets().subscribe({
      next: wallets => this.loadRows(wallets),
      error: () => {
        this.isLoading.set(false);
        this.loadFailed.set(true);
      },
    });
  }

  private loadRows(wallets: PointsWalletDto[]): void {
    const targets = wallets
      .filter((w): w is PointsWalletDto & { tenantId: string } => !!w.tenantId)
      .slice(0, WALLET_FANOUT);

    this.hasWallets.set(targets.length > 0);
    if (targets.length === 0) {
      this.rows.set([]);
      this.isLoading.set(false);
      return;
    }

    const type = this.filter();
    const requests = targets.map(wallet =>
      this.walletService
        .getMyTransactionHistory(wallet.tenantId, {
          type,
          sorting: 'creationTime desc',
          skipCount: 0,
          maxResultCount: ROWS_PER_WALLET,
        })
        .pipe(
          map(result =>
            (result.items ?? []).map(
              (transaction): ActivityRow => ({ transaction, businessName: wallet.businessName ?? '' }),
            ),
          ),
          // One business being unreachable should not blank the rest of the list.
          catchError(() => of<ActivityRow[]>([])),
        ),
    );

    forkJoin(requests).subscribe(perWallet => {
      const merged = perWallet
        .flat()
        .sort((a, b) => new Date(b.transaction.creationTime ?? 0).getTime() - new Date(a.transaction.creationTime ?? 0).getTime())
        .slice(0, PREVIEW_LIMIT);
      this.rows.set(merged);
      this.isLoading.set(false);
    });
  }
}
