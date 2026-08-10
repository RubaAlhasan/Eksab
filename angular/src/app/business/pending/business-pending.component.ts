import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { AuthService, LocalizationPipe } from '@abp/ng.core';
import { BusinessService } from '../../proxy/controllers/business.service';
import { TenantApprovalStatus } from '../../proxy/business-profiles/tenant-approval-status.enum';

/**
 * Landing page for a business account that `businessApprovalGuard` (core/guards/business.guard.ts)
 * redirected here instead of into the portal — `BusinessProfile.ApprovalStatus` is `Pending` (not yet
 * reviewed) or `Suspended` (reviewed, then blocked). Deliberately a bare standalone route (not a child
 * of `business`, not wrapped in `BusinessLayoutComponent`'s sidebar) — every nav link in that shell
 * leads to a page real functionality shouldn't be reachable from yet, so there's nothing to show but
 * this message and a way out.
 *
 * Re-fetches the profile itself rather than receiving status via route state, for the same reason
 * `BusinessSettingsComponent` always re-fetches: a direct/refreshed navigation to this URL has no
 * router state to read, and this is the one place staleness (an admin approving/suspending between
 * the guard's check and this page rendering) would visibly mislead someone.
 */
@Component({
  selector: 'app-business-pending',
  templateUrl: './business-pending.component.html',
  styleUrls: ['./business-pending.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [LocalizationPipe],
})
export class BusinessPendingComponent implements OnInit {
  private readonly businessService = inject(BusinessService);
  private readonly authService = inject(AuthService);

  protected readonly TenantApprovalStatus = TenantApprovalStatus;
  protected readonly isLoading = signal(true);
  protected readonly status = signal<TenantApprovalStatus | null>(null);

  ngOnInit(): void {
    this.businessService.getProfile().subscribe({
      next: profile => {
        this.status.set(profile.approvalStatus);
        this.isLoading.set(false);
      },
      // Same "fail open on error" reasoning as the guard itself — if the profile call fails here too,
      // default to the Pending message rather than leaving the page stuck loading forever.
      error: () => {
        this.status.set(TenantApprovalStatus.Pending);
        this.isLoading.set(false);
      },
    });
  }

  protected logout(): void {
    this.authService.logout().subscribe();
  }
}
