import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { MembershipsService } from '../../proxy/controllers/memberships.service';
import { WalletService } from '../../proxy/controllers/wallet.service';
import type { PointsWalletDto } from '../../proxy/wallets/models';
import type { TransactionListItemDto } from '../../proxy/reports/models';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../shared/components/error-state/error-state.component';
import { AnimatedNumberComponent } from '../../shared/components/animated-number/animated-number.component';
import { TransactionDetailModalComponent } from '../../shared/components/transaction-detail-modal/transaction-detail-modal.component';
import { isCredit, transactionSourceLabelKey, transactionTypeLabelKey } from '../../shared/utils/transaction-display.util';

/**
 * "My Points" — a single business's wallet detail. Keyed by `tenantId` (not `membershipId`/`walletId`)
 * since that's what every downstream endpoint (transaction history, reward catalog, redeem) is actually
 * keyed by; the wallet itself has no by-id getter so it's filtered from `getMyWallets()`.
 *
 * The tier-progress bar reads `PointsWalletDto`'s own tier fields (current tier's floor, next tier's name
 * and floor) rather than calling `TiersController`, which stays gated on `Eksabli.Tiers.Default`, a
 * staff-only permission a customer account never holds. `GetMyWalletsAsync` already resolves those fields.
 */
@Component({
  selector: 'app-customer-points',
  templateUrl: './customer-points.component.html',
  styleUrls: ['./customer-points.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, DatePipe, DecimalPipe, LocalizationPipe, EmptyStateComponent, ErrorStateComponent, TransactionDetailModalComponent, AnimatedNumberComponent],
})
export class CustomerPointsComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly membershipsService = inject(MembershipsService);
  private readonly walletService = inject(WalletService);

  // Not a route-param-driven signal/field-initializer snapshot read — Angular's default route reuse
  // strategy can keep this component instance alive across a same-route, different-param navigation
  // (same shape as business-customer-details.component.ts's own `route.paramMap.subscribe`), so this is
  // re-read on every params emission rather than once at construction.
  protected tenantId = '';

  protected readonly isLoading = signal(true);
  protected readonly loadFailed = signal(false);
  protected readonly wallet = signal<PointsWalletDto | null>(null);
  protected readonly recentActivity = signal<TransactionListItemDto[]>([]);

  protected readonly walletNotFound = computed(() => !this.isLoading() && !this.loadFailed() && !this.wallet());
  protected readonly reservedPoints = computed(() => this.wallet()?.reserved ?? 0);

  // Progress from where the current tier starts to where the next one begins, measured on lifetime points, the same
  // number the tiers are awarded on. The bar starts at the current tier's floor, not zero, so a customer who has just
  // reached a tier does not look nearly done. Null when this business has no next tier to show.
  protected readonly tierProgress = computed(() => {
    const wallet = this.wallet();
    if (!wallet?.nextTierName || wallet.nextTierMinLifetimePoints == null) return null;

    const lifetime = wallet.lifetimeEarned ?? 0;
    const floor = wallet.currentTierMinLifetimePoints ?? 0;
    const target = wallet.nextTierMinLifetimePoints;
    const span = Math.max(1, target - floor);
    const progressed = Math.min(span, Math.max(0, lifetime - floor));

    return {
      percent: Math.round((progressed / span) * 100),
      remaining: Math.max(0, target - lifetime),
      nextName: wallet.nextTierName,
    };
  });

  protected readonly typeLabelKey = transactionTypeLabelKey;
  protected readonly sourceLabelKey = transactionSourceLabelKey;
  protected readonly isCredit = isCredit;

  // Same "one row per process, click for the breakdown" pattern as the full Transaction History page
  // (customer-transaction-history.component.ts) — this preview showed each row's fields inline with no
  // way to see the per-component breakdown behind a multi-component row.
  protected readonly selectedTransaction = signal<TransactionListItemDto | null>(null);
  protected readonly transactionDetailsOpen = signal(false);

  // "Leave this business" — same inline two-step confirm shape customer-redeem.component.ts already
  // uses for its own Cancel action, rather than a native confirm() dialog.
  protected readonly showLeaveConfirm = signal(false);
  protected readonly isLeaving = signal(false);
  protected readonly leaveFailed = signal(false);

  ngOnInit(): void {
    this.route.paramMap.subscribe(params => {
      const tenantId = params.get('tenantId');
      if (!tenantId) return;
      this.tenantId = tenantId;
      this.load(tenantId);
    });
  }

  protected retry(): void {
    if (this.tenantId) this.load(this.tenantId);
  }

  protected openTransactionDetails(txn: TransactionListItemDto): void {
    this.selectedTransaction.set(txn);
    this.transactionDetailsOpen.set(true);
  }

  protected closeTransactionDetails(): void {
    this.transactionDetailsOpen.set(false);
  }

  protected beginLeave(): void {
    this.leaveFailed.set(false);
    this.showLeaveConfirm.set(true);
  }

  protected cancelLeave(): void {
    this.showLeaveConfirm.set(false);
  }

  protected confirmLeave(): void {
    if (this.isLeaving() || !this.tenantId) return;
    this.isLeaving.set(true);
    this.leaveFailed.set(false);
    this.membershipsService.leave(this.tenantId).subscribe({
      next: () => {
        this.isLeaving.set(false);
        // The wallet this page shows no longer exists in getMyWallets() once membership is Cancelled
        // (see MembershipAppService.GetMyWalletsAsync) — nothing left here worth staying on.
        void this.router.navigate(['/customer/home']);
      },
      // The interceptor already surfaces the server's own message — same idiom used elsewhere in this
      // app for expected, user-facing failures.
      error: () => {
        this.isLeaving.set(false);
        this.leaveFailed.set(true);
      },
    });
  }

  private load(tenantId: string): void {
    this.isLoading.set(true);
    this.loadFailed.set(false);
    // Reset before either request resolves: this component is reused across a same-route, different-business
    // navigation (see tenantId's own comment above), and without this the previous business's recent activity
    // could flash on screen until this business's own history call resolves.
    this.recentActivity.set([]);

    this.membershipsService.getMyWallets().subscribe({
      next: wallets => {
        this.wallet.set(wallets.find(w => w.tenantId === tenantId) ?? null);
        this.isLoading.set(false);
      },
      error: () => {
        this.isLoading.set(false);
        this.loadFailed.set(true);
      },
    });

    this.walletService
      .getMyTransactionHistory(tenantId, { maxResultCount: 5, sorting: 'creationTime desc', skipCount: 0 })
      .subscribe({
        next: result => this.recentActivity.set(result.items ?? []),
        error: () => undefined,
      });
  }
}
