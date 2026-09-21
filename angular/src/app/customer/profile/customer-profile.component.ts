import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService, ConfigStateService, LocalizationPipe } from '@abp/ng.core';
import { CustomerProfileService } from '../../proxy/controllers/customer-profile.service';
import { LoadingSpinnerComponent } from '../../shared/components/loading-spinner/loading-spinner.component';
import { ErrorStateComponent } from '../../shared/components/error-state/error-state.component';

@Component({
  selector: 'app-customer-profile',
  templateUrl: './customer-profile.component.html',
  styleUrls: ['./customer-profile.component.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, LocalizationPipe, LoadingSpinnerComponent, ErrorStateComponent],
})
export class CustomerProfileComponent implements OnInit {
  private readonly customerProfileService = inject(CustomerProfileService);
  private readonly configState = inject(ConfigStateService);
  private readonly authService = inject(AuthService);

  protected readonly isLoading = signal(true);
  protected readonly loadFailed = signal(false);
  protected readonly displayName = signal<string | null>(null);

  private readonly currentUser = computed(
    () => this.configState.getOne('currentUser') as { phoneNumber?: string; email?: string } | undefined,
  );
  protected readonly phoneNumber = computed(() => this.currentUser()?.phoneNumber ?? null);
  protected readonly email = computed(() => this.currentUser()?.email ?? null);

  ngOnInit(): void {
    this.load();
  }

  protected retry(): void {
    this.load();
  }

  protected logout(): void {
    this.authService.logout().subscribe();
  }

  private load(): void {
    this.isLoading.set(true);
    this.loadFailed.set(false);
    this.customerProfileService.getMyProfile().subscribe({
      next: profile => {
        const fullName = [profile.firstName, profile.lastName].filter(Boolean).join(' ').trim();
        this.displayName.set(fullName || null);
        this.isLoading.set(false);
      },
      error: () => {
        this.isLoading.set(false);
        this.loadFailed.set(true);
      },
    });
  }
}
