import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ConfigStateService, LocalizationPipe } from '@abp/ng.core';
import { MembershipsService } from '../../proxy/controllers/memberships.service';
import { CustomerProfileService } from '../../proxy/controllers/customer-profile.service';
import type { PointsWalletDto } from '../../proxy/wallets/models';
import { LoadingSpinnerComponent } from '../../shared/components/loading-spinner/loading-spinner.component';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../shared/components/error-state/error-state.component';

/**
 * Customer app Home tab — every business the customer has joined, each with its own independent
 * points balance/tier, from `MembershipAppService.GetMyWalletsAsync`. Moved+renamed from the original
 * `home/home.component.ts` (now the index route of the `CustomerLayoutComponent` shell at
 * `/customer/home`) as part of building out the rest of the wallet flow — each wallet card is now a
 * real drill-in link instead of a dead end, and a "Show My QR" entry point was added since the wallet
 * QR (`MembershipsService.getMyWalletQrToken`) is one-per-account, not tied to any single business.
 */
@Component({
  selector: 'app-customer-home',
  templateUrl: './customer-home.component.html',
  styleUrls: ['./customer-home.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, LocalizationPipe, LoadingSpinnerComponent, EmptyStateComponent, ErrorStateComponent],
})
export class CustomerHomeComponent implements OnInit {
  private readonly membershipsService = inject(MembershipsService);
  private readonly customerProfileService = inject(CustomerProfileService);
  private readonly configState = inject(ConfigStateService);

  protected readonly isLoading = signal(true);
  protected readonly loadFailed = signal(false);
  protected readonly wallets = signal<PointsWalletDto[]>([]);
  protected readonly displayName = signal<string | null>(null);

  // Falls back to the phone number on the token itself (CurrentUserDto.phoneNumber) for a brand-new
  // customer whose CustomerProfile has no name yet (OtpLoginService's auto-create-blank-profile
  // fallback).
  protected readonly greetingName = computed(() => {
    const name = this.displayName();
    if (name) return name;
    const currentUser = this.configState.getOne('currentUser') as { phoneNumber?: string } | undefined;
    return currentUser?.phoneNumber ?? '';
  });

  ngOnInit(): void {
    this.customerProfileService.getMyProfile().subscribe({
      next: profile => {
        const fullName = [profile.firstName, profile.lastName].filter(Boolean).join(' ').trim();
        this.displayName.set(fullName || null);
      },
      error: () => undefined,
    });

    this.load();
  }

  protected retry(): void {
    this.load();
  }

  private load(): void {
    this.isLoading.set(true);
    this.loadFailed.set(false);
    this.membershipsService.getMyWallets().subscribe({
      next: wallets => {
        this.wallets.set(wallets);
        this.isLoading.set(false);
      },
      error: () => {
        this.isLoading.set(false);
        this.loadFailed.set(true);
      },
    });
  }
}
