import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { WalletService } from '../../proxy/controllers/wallet.service';
import type { PointsTransactionDto } from '../../proxy/wallets/models';
import { LoadingSpinnerComponent } from '../../shared/components/loading-spinner/loading-spinner.component';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../shared/components/error-state/error-state.component';
import { PaginationComponent } from '../../shared/components/pagination/pagination.component';
import { isCredit, transactionSourceLabelKey, transactionTypeLabelKey } from '../../shared/utils/transaction-display.util';

@Component({
  selector: 'app-customer-transaction-history',
  templateUrl: './customer-transaction-history.component.html',
  styleUrls: ['./customer-transaction-history.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, LocalizationPipe, LoadingSpinnerComponent, EmptyStateComponent, ErrorStateComponent, PaginationComponent],
})
export class CustomerTransactionHistoryComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly walletService = inject(WalletService);

  private readonly pageSize = 10;
  // See customer-points.component.ts's identical comment on why this isn't a snapshot field initializer.
  protected tenantId = '';

  protected readonly transactions = signal<PointsTransactionDto[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly isLoading = signal(true);
  protected readonly loadFailed = signal(false);
  protected readonly pageIndex = signal(0);
  protected readonly totalPages = computed(() => Math.max(1, Math.ceil(this.totalCount() / this.pageSize)));

  protected readonly typeLabelKey = transactionTypeLabelKey;
  protected readonly sourceLabelKey = transactionSourceLabelKey;
  protected readonly isCredit = isCredit;

  ngOnInit(): void {
    this.route.paramMap.subscribe(params => {
      const tenantId = params.get('tenantId');
      if (!tenantId) return;
      this.tenantId = tenantId;
      this.pageIndex.set(0);
      this.load(tenantId);
    });
  }

  protected retry(): void {
    if (this.tenantId) this.load(this.tenantId);
  }

  protected goToPage(index: number): void {
    if (index < 0 || index >= this.totalPages() || !this.tenantId) return;
    this.pageIndex.set(index);
    this.load(this.tenantId);
  }

  private load(tenantId: string): void {
    this.isLoading.set(true);
    this.loadFailed.set(false);

    this.walletService
      .getMyTransactionHistory(tenantId, {
        sorting: 'creationTime desc',
        skipCount: this.pageIndex() * this.pageSize,
        maxResultCount: this.pageSize,
      })
      .subscribe({
        next: result => {
          this.transactions.set(result.items ?? []);
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
