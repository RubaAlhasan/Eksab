import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { Router } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { CouponsService } from '../../proxy/controllers/coupons.service';
import type { CouponDto } from '../../proxy/rewards/models';
import { CouponStatus } from '../../proxy/rewards/coupon-status.enum';
import { SkeletonListComponent } from '../../shared/components/skeleton-list/skeleton-list.component';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../shared/components/error-state/error-state.component';
import { StatusBadgeComponent, StatusBadgeVariant } from '../../shared/components/status-badge/status-badge.component';

/**
 * "My Coupons" across all businesses. Unlike `business-coupons.component.ts` (the staff audit trail,
 * which folds Pending into the same badge as Issued by omission), Pending gets its own distinct
 * "Awaiting approval" badge here — it matters to the customer whether staff have actually confirmed the
 * redemption yet. Tapping a Pending row re-enters the redeem screen (resumes polling), which also
 * doubles as the recovery path after navigating away or refreshing the tab mid-redemption.
 *
 * Filter tabs match the prototype's coupons.html (All/Active/Used/Expired) — adapted for this app's
 * richer, real status model (prototype only ever modelled Issued/Redeemed/Expired/Cancelled, no Pending
 * concept at all): Active folds in Pending alongside Issued (both are "not yet used, not expired" from
 * the customer's point of view), and Expired folds in Cancelled — the prototype's own `statusMeta`
 * already groups Cancelled under the same danger-styled badge as Expired, so this mirrors that.
 */
type CouponFilter = 'all' | 'active' | 'used' | 'expired';
@Component({
  selector: 'app-customer-my-coupons',
  templateUrl: './customer-my-coupons.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, LocalizationPipe, SkeletonListComponent, EmptyStateComponent, ErrorStateComponent, StatusBadgeComponent],
})
export class CustomerMyCouponsComponent implements OnInit {
  private readonly router = inject(Router);
  private readonly couponsService = inject(CouponsService);

  protected readonly Status = CouponStatus;
  protected readonly coupons = signal<CouponDto[]>([]);
  protected readonly isLoading = signal(true);
  protected readonly loadFailed = signal(false);

  protected readonly filter = signal<CouponFilter>('all');

  protected readonly filteredCoupons = computed(() => {
    const list = this.coupons();
    switch (this.filter()) {
      case 'active':
        return list.filter(c => c.status === CouponStatus.Issued || c.status === CouponStatus.Pending);
      case 'used':
        return list.filter(c => c.status === CouponStatus.Redeemed);
      case 'expired':
        return list.filter(c => c.status === CouponStatus.Expired || c.status === CouponStatus.Cancelled);
      default:
        return list;
    }
  });

  ngOnInit(): void {
    this.load();
  }

  protected retry(): void {
    this.load();
  }

  protected selectFilter(filter: CouponFilter): void {
    this.filter.set(filter);
  }

  protected onCouponClick(coupon: CouponDto): void {
    if (coupon.status !== CouponStatus.Pending || !coupon.tenantId || !coupon.id) return;
    void this.router.navigate(['/customer/redeem', coupon.tenantId, coupon.id]);
  }

  protected statusLabelKey(status: CouponStatus | undefined): string {
    switch (status) {
      case CouponStatus.Pending:
        return '::Wallet:Coupons:StatusPending';
      case CouponStatus.Redeemed:
        return '::Wallet:Coupons:StatusRedeemed';
      case CouponStatus.Expired:
        return '::Wallet:Coupons:StatusExpired';
      case CouponStatus.Cancelled:
        return '::Wallet:Coupons:StatusCancelled';
      default:
        return '::Wallet:Coupons:StatusIssued';
    }
  }

  protected statusVariant(status: CouponStatus | undefined): StatusBadgeVariant {
    switch (status) {
      case CouponStatus.Pending:
        return 'warning';
      case CouponStatus.Redeemed:
        return 'neutral';
      case CouponStatus.Expired:
      case CouponStatus.Cancelled:
        return 'danger';
      default:
        return 'success';
    }
  }

  private load(): void {
    this.isLoading.set(true);
    this.loadFailed.set(false);
    this.couponsService.getMyCoupons().subscribe({
      next: coupons => {
        this.coupons.set(coupons);
        this.isLoading.set(false);
      },
      error: () => {
        this.isLoading.set(false);
        this.loadFailed.set(true);
      },
    });
  }
}
