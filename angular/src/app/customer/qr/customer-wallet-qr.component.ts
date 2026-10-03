import { ChangeDetectionStrategy, Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { LocalizationPipe } from '@abp/ng.core';
import { MembershipsService } from '../../proxy/controllers/memberships.service';
import { SkeletonListComponent } from '../../shared/components/skeleton-list/skeleton-list.component';
import { ErrorStateComponent } from '../../shared/components/error-state/error-state.component';
import { QrCodeComponent } from '../../shared/components/qr-code/qr-code.component';

/**
 * "My Wallet QR" — one QR per customer account (not per business, see `MembershipsService
 * .getMyWalletQrToken`'s own `[no params]` signature), shown to staff to identify the customer and
 * award points to whichever business they're standing in. Server-side TTL is 90 seconds
 * (`MembershipAppService.GetMyWalletQrTokenAsync`) — short enough that this auto-refreshes on expiry
 * rather than just displaying a dead code, plus a manual "Refresh" button for an early re-issue.
 */
@Component({
  selector: 'app-customer-wallet-qr',
  templateUrl: './customer-wallet-qr.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [LocalizationPipe, SkeletonListComponent, ErrorStateComponent, QrCodeComponent],
})
export class CustomerWalletQrComponent implements OnInit, OnDestroy {
  private readonly membershipsService = inject(MembershipsService);

  protected readonly isLoading = signal(true);
  protected readonly loadFailed = signal(false);
  protected readonly token = signal<string | null>(null);

  protected readonly secondsLeft = signal<number | null>(null);
  private countdownHandle: ReturnType<typeof setInterval> | null = null;

  protected readonly countdownLabel = computed(() => {
    const left = this.secondsLeft();
    if (left === null) return null;
    return Math.max(0, left).toString();
  });

  ngOnInit(): void {
    this.fetchToken(true);
  }

  ngOnDestroy(): void {
    this.stopCountdown();
  }

  protected retry(): void {
    this.fetchToken(true);
  }

  protected refresh(): void {
    this.fetchToken(false);
  }

  private fetchToken(isFirstLoad: boolean): void {
    if (isFirstLoad) {
      this.isLoading.set(true);
      this.loadFailed.set(false);
    }
    this.membershipsService.getMyWalletQrToken().subscribe({
      next: result => {
        this.isLoading.set(false);
        this.token.set(result.token ?? null);
        this.startCountdown(result.expiresInSeconds ?? null);
      },
      error: () => {
        this.isLoading.set(false);
        if (isFirstLoad) this.loadFailed.set(true);
      },
    });
  }

  private startCountdown(totalSeconds: number | null): void {
    this.stopCountdown();
    if (totalSeconds == null) {
      this.secondsLeft.set(null);
      return;
    }

    const deadline = Date.now() + totalSeconds * 1000;
    const tick = () => {
      const remaining = Math.round((deadline - Date.now()) / 1000);
      this.secondsLeft.set(Math.max(0, remaining));
      if (remaining <= 0) {
        this.stopCountdown();
        // A customer handing their phone to a cashier shouldn't need to notice/tap a button mid-queue.
        this.fetchToken(false);
      }
    };
    tick();
    this.countdownHandle = setInterval(tick, 1000);
  }

  private stopCountdown(): void {
    if (this.countdownHandle !== null) {
      clearInterval(this.countdownHandle);
      this.countdownHandle = null;
    }
    this.secondsLeft.set(null);
  }
}
