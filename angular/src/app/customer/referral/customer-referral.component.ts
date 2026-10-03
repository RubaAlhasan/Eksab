import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { LocalizationPipe } from '@abp/ng.core';
import { ToasterService } from '@abp/ng.theme.shared';
import { MembershipsService } from '../../proxy/controllers/memberships.service';
import { ReferralsService } from '../../proxy/controllers/referrals.service';
import type { PointsWalletDto } from '../../proxy/wallets/models';
import type { ReferralDto } from '../../proxy/engagement/models';
import { ReferralStatus } from '../../proxy/engagement/referral-status.enum';
import { LoadingSpinnerComponent } from '../../shared/components/loading-spinner/loading-spinner.component';
import { SkeletonListComponent } from '../../shared/components/skeleton-list/skeleton-list.component';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { StatusBadgeComponent, StatusBadgeVariant } from '../../shared/components/status-badge/status-badge.component';

/**
 * Refer a Friend — scoped per business (`ReferralsService.getMyReferralCode` 404s if the caller isn't a
 * member of the chosen business yet, confirmed by reading `IReferralAppService`'s own comment: "only
 * existing members can refer others"), so this page has a business picker rather than one global code
 * like the prototype's single-business mockup implied.
 *
 * `ReferralDto` carries no referee name (just a bare `refereeCustomerId`, with no lookup a plain
 * customer account can call) — history rows show status + date + which business, not who.
 */
@Component({
  selector: 'app-customer-referral',
  templateUrl: './customer-referral.component.html',
  styleUrls: ['./customer-referral.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, LocalizationPipe, LoadingSpinnerComponent, SkeletonListComponent, EmptyStateComponent, StatusBadgeComponent],
})
export class CustomerReferralComponent implements OnInit {
  private readonly membershipsService = inject(MembershipsService);
  private readonly referralsService = inject(ReferralsService);
  private readonly toaster = inject(ToasterService);

  protected readonly Status = ReferralStatus;
  protected readonly wallets = signal<PointsWalletDto[]>([]);
  protected readonly selectedTenantId = signal<string | null>(null);
  protected readonly code = signal<string | null>(null);
  protected readonly isLoadingCode = signal(false);

  protected readonly referrals = signal<ReferralDto[]>([]);
  protected readonly isLoading = signal(true);

  protected readonly businessNameByTenantId = computed(() => {
    const map = new Map<string, string>();
    for (const wallet of this.wallets()) {
      if (wallet.tenantId && wallet.businessName) map.set(wallet.tenantId, wallet.businessName);
    }
    return map;
  });

  protected readonly completedCount = computed(
    () => this.referrals().filter(r => r.status === ReferralStatus.Completed || r.status === ReferralStatus.Rewarded).length,
  );
  protected readonly pendingCount = computed(() => this.referrals().filter(r => r.status === ReferralStatus.Pending).length);

  ngOnInit(): void {
    this.isLoading.set(true);
    this.membershipsService.getMyWallets().subscribe({
      next: wallets => {
        this.wallets.set(wallets);
        const firstTenantId = wallets[0]?.tenantId ?? null;
        if (firstTenantId) this.selectBusiness(firstTenantId);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false),
    });

    this.referralsService.getMyReferrals().subscribe({
      next: referrals => this.referrals.set(referrals),
      error: () => undefined,
    });
  }

  protected onBusinessChange(event: Event): void {
    this.selectBusiness((event.target as HTMLSelectElement).value);
  }

  protected businessName(referral: ReferralDto): string {
    return (referral.tenantId && this.businessNameByTenantId().get(referral.tenantId)) || '—';
  }

  protected statusLabelKey(status: ReferralStatus | undefined): string {
    switch (status) {
      case ReferralStatus.Completed:
        return '::Wallet:Referral:StatusCompleted';
      case ReferralStatus.Rewarded:
        return '::Wallet:Referral:StatusRewarded';
      default:
        return '::Wallet:Referral:StatusPending';
    }
  }

  protected statusVariant(status: ReferralStatus | undefined): StatusBadgeVariant {
    switch (status) {
      case ReferralStatus.Completed:
      case ReferralStatus.Rewarded:
        return 'success';
      default:
        return 'warning';
    }
  }

  protected copyCode(): void {
    const code = this.code();
    if (!code) return;
    navigator.clipboard
      ?.writeText(code)
      .then(() => this.toaster.success('::Wallet:Referral:CopiedMessage'))
      .catch(() => undefined);
  }

  protected share(): void {
    const code = this.code();
    if (!code) return;
    const text = `${code}`;
    if (navigator.share) {
      navigator.share({ text }).catch(() => undefined);
    } else {
      this.copyCode();
    }
  }

  private selectBusiness(tenantId: string): void {
    this.selectedTenantId.set(tenantId);
    this.code.set(null);
    this.isLoadingCode.set(true);
    this.referralsService.getMyReferralCode(tenantId).subscribe({
      next: result => {
        this.code.set(result.code ?? null);
        this.isLoadingCode.set(false);
      },
      error: () => this.isLoadingCode.set(false),
    });
  }
}
