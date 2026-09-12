import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { ConfigStateService, LocalizationPipe } from '@abp/ng.core';
import { MembershipsService } from '../proxy/controllers/memberships.service';
import { CustomerProfileService } from '../proxy/controllers/customer-profile.service';
import type { PointsWalletDto } from '../proxy/wallets/models';

/**
 * Real landing page for a Host-realm customer who signed in from the web (see
 * customer-login.component.ts) instead of the not-yet-built Flutter app — every business they've
 * joined, each with its own independent points balance/tier, exactly what `MembershipAppService
 * .GetMyWalletsAsync` already returns (one wallet per membership; `BusinessName`/`CurrentTierName`
 * resolved server-side, see that method's own comment). Replaces the previous bare "Welcome to
 * Eksabli" placeholder — a platform admin or business staff account never reaches this route in
 * practice (redirectAuthenticatedToHomeGuard in app.routes.ts sends them to /admin or /business
 * instead), so this can assume a customer without re-checking realm itself.
 */
@Component({
  selector: 'app-home',
  templateUrl: './home.component.html',
  styleUrls: ['./home.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [LocalizationPipe],
})
export class HomeComponent implements OnInit {
  private readonly membershipsService = inject(MembershipsService);
  private readonly customerProfileService = inject(CustomerProfileService);
  private readonly configState = inject(ConfigStateService);

  protected readonly isLoading = signal(true);
  protected readonly wallets = signal<PointsWalletDto[]>([]);
  protected readonly displayName = signal<string | null>(null);

  // Falls back to the phone number on the token itself (CurrentUserDto.phoneNumber, from
  // ConfigStateService — same "as {...}" shape business-support-tickets.component.ts already uses
  // for currentUser.id) for a brand-new customer whose CustomerProfile has no name yet (OtpLoginService's
  // auto-create-blank-profile fallback — see that file's own comment).
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

    this.membershipsService.getMyWallets().subscribe({
      next: wallets => {
        this.wallets.set(wallets);
        this.isLoading.set(false);
      },
      error: () => this.isLoading.set(false),
    });
  }
}
