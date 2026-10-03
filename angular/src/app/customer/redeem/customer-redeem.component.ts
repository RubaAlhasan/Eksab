import { ChangeDetectionStrategy, Component, OnDestroy, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { CouponsService } from '../../proxy/controllers/coupons.service';
import type { CouponDto } from '../../proxy/rewards/models';
import { CouponStatus } from '../../proxy/rewards/coupon-status.enum';
import { SkeletonListComponent } from '../../shared/components/skeleton-list/skeleton-list.component';
import { ErrorStateComponent } from '../../shared/components/error-state/error-state.component';
import { QrCodeComponent } from '../../shared/components/qr-code/qr-code.component';

const POLL_INTERVAL_MS = 5000;
// The 15-minute reservation window (CouponConsts.PendingWindowMinutes) plus slack for
// RedemptionReservationWorker's own sweep cycle to actually flip the status — polling past this without
// resolution means something's stuck, not that the code is still legitimately pending.
const POLL_SAFETY_CAP_MS = 20 * 60 * 1000;

/**
 * Reserve → show token → wait-for-staff-confirmation screen. Always fetches by `tenantId`+`couponId`
 * route params (never router state), so it's refresh-safe and reachable both right after reserving a
 * reward and later from "My Coupons" (a still-Pending coupon links back here to resume).
 *
 * The QR payload IS the coupon's code — not a separate token — matching exactly what
 * `business-redemption.component.ts` (the staff-side counterpart) already expects to scan.
 */
@Component({
  selector: 'app-customer-redeem',
  templateUrl: './customer-redeem.component.html',
  styleUrls: ['./customer-redeem.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, LocalizationPipe, SkeletonListComponent, ErrorStateComponent, QrCodeComponent],
})
export class CustomerRedeemComponent implements OnInit, OnDestroy {
  private readonly route = inject(ActivatedRoute);
  private readonly couponsService = inject(CouponsService);

  // See customer-points.component.ts's identical comment on why these aren't snapshot field
  // initializers.
  private tenantId = '';
  private couponId = '';

  protected readonly Status = CouponStatus;
  protected readonly isLoading = signal(true);
  protected readonly loadFailed = signal(false);
  protected readonly coupon = signal<CouponDto | null>(null);
  protected readonly isCancelling = signal(false);
  protected readonly showCancelConfirm = signal(false);
  protected readonly pollTimedOut = signal(false);

  protected readonly secondsLeft = signal<number | null>(null);
  private countdownHandle: ReturnType<typeof setInterval> | null = null;
  private pollHandle: ReturnType<typeof setInterval> | null = null;
  private pollStartedAt = 0;

  protected readonly countdownLabel = computed(() => {
    const left = this.secondsLeft();
    if (left === null) return null;
    if (left <= 0) return '00:00';
    const minutes = Math.floor(left / 60).toString().padStart(2, '0');
    const seconds = (left % 60).toString().padStart(2, '0');
    return `${minutes}:${seconds}`;
  });

  ngOnInit(): void {
    this.route.paramMap.subscribe(params => {
      const tenantId = params.get('tenantId');
      const couponId = params.get('couponId');
      if (!tenantId || !couponId) return;
      this.stopCountdown();
      this.stopPolling();
      this.tenantId = tenantId;
      this.couponId = couponId;
      this.pollTimedOut.set(false);
      this.showCancelConfirm.set(false);
      this.fetch(true);
    });
  }

  ngOnDestroy(): void {
    this.stopCountdown();
    this.stopPolling();
  }

  /** Display-only grouping — matches business-redemption.component.ts's identical formatCode(). */
  protected formatCode(code: string | null | undefined): string {
    if (!code) return '';
    return code.length === 8 ? `${code.slice(0, 4)} ${code.slice(4)}` : code;
  }

  protected retry(): void {
    this.fetch(true);
  }

  protected beginCancel(): void {
    this.showCancelConfirm.set(true);
  }

  protected dismissCancel(): void {
    this.showCancelConfirm.set(false);
  }

  protected confirmCancel(): void {
    if (this.isCancelling()) return;
    this.isCancelling.set(true);
    this.couponsService.cancelMyCoupon(this.tenantId, this.couponId).subscribe({
      next: coupon => {
        this.isCancelling.set(false);
        this.showCancelConfirm.set(false);
        this.applyCoupon(coupon);
      },
      error: () => this.isCancelling.set(false),
    });
  }

  private fetch(isFirstLoad: boolean): void {
    if (isFirstLoad) {
      this.isLoading.set(true);
      this.loadFailed.set(false);
    }
    this.couponsService.getMyCoupon(this.tenantId, this.couponId).subscribe({
      next: coupon => {
        this.isLoading.set(false);
        this.applyCoupon(coupon);
      },
      error: () => {
        this.isLoading.set(false);
        if (isFirstLoad) this.loadFailed.set(true);
      },
    });
  }

  private applyCoupon(coupon: CouponDto): void {
    this.coupon.set(coupon);

    if (coupon.status !== CouponStatus.Pending) {
      this.stopCountdown();
      this.stopPolling();
      return;
    }

    this.startCountdown(coupon.reservationExpiresAt);
    this.startPollingIfNeeded();
  }

  private startPollingIfNeeded(): void {
    if (this.pollHandle !== null) return;
    this.pollStartedAt = Date.now();
    this.pollHandle = setInterval(() => {
      if (Date.now() - this.pollStartedAt >= POLL_SAFETY_CAP_MS) {
        this.pollTimedOut.set(true);
        this.stopPolling();
        return;
      }
      this.fetch(false);
    }, POLL_INTERVAL_MS);
  }

  private stopPolling(): void {
    if (this.pollHandle !== null) {
      clearInterval(this.pollHandle);
      this.pollHandle = null;
    }
  }

  // Same UTC-offset-marker handling as business-redemption.component.ts's startCountdown — the API
  // stores an offset-less UTC timestamp, which ECMAScript would otherwise misread as local time.
  private startCountdown(expiresAt: string | null | undefined): void {
    this.stopCountdown();
    if (!expiresAt) {
      this.secondsLeft.set(null);
      return;
    }

    const deadline = Date.parse(/[Z+]|-\d{2}:?\d{2}$/.test(expiresAt) ? expiresAt : `${expiresAt}Z`);
    if (Number.isNaN(deadline)) {
      this.secondsLeft.set(null);
      return;
    }

    const tick = () => this.secondsLeft.set(Math.max(0, Math.round((deadline - Date.now()) / 1000)));
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
