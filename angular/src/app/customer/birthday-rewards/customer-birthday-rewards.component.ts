import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { LocalizedNamePipe } from '../../shared/pipes/localized-name.pipe';
import { CustomerCampaignService } from '../../proxy/controllers/customer-campaign.service';
import { CustomerProfileService } from '../../proxy/controllers/customer-profile.service';
import { CampaignType } from '../../proxy/campaigns/campaign-type.enum';
import type { CustomerCampaignDto } from '../../proxy/campaigns/models';
import { SkeletonListComponent } from '../../shared/components/skeleton-list/skeleton-list.component';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../shared/components/error-state/error-state.component';

/**
 * Prototype's birthday-rewards.html, reached from the Profile menu — previously missing entirely
 * (unlike this app's other intentional prototype cuts, nothing justified the gap: the data is already
 * flowing through `CustomerCampaignService.getMyFeed()` elsewhere in the app, this is just that same
 * feed filtered to `CampaignType.Birthday`). The prototype's banner hardcodes a birthday date from demo
 * data; here it only renders when the real profile actually has one set (`dateOfBirth` is optional —
 * see `CustomerEditProfileComponent`), rather than showing a misleading placeholder.
 */
@Component({
  selector: 'app-customer-birthday-rewards',
  templateUrl: './customer-birthday-rewards.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, DatePipe, LocalizationPipe, LocalizedNamePipe, SkeletonListComponent, EmptyStateComponent, ErrorStateComponent],
})
export class CustomerBirthdayRewardsComponent implements OnInit {
  private readonly customerCampaignService = inject(CustomerCampaignService);
  private readonly customerProfileService = inject(CustomerProfileService);

  protected readonly isLoading = signal(true);
  protected readonly loadFailed = signal(false);
  protected readonly campaigns = signal<CustomerCampaignDto[]>([]);
  protected readonly dateOfBirth = signal<string | null>(null);

  protected readonly birthdayCampaigns = computed(() => this.campaigns().filter(c => c.type === CampaignType.Birthday));

  ngOnInit(): void {
    this.customerProfileService.getMyProfile().subscribe({
      next: profile => this.dateOfBirth.set(profile.dateOfBirth ?? null),
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
    this.customerCampaignService.getMyFeed().subscribe({
      next: campaigns => {
        this.campaigns.set(campaigns);
        this.isLoading.set(false);
      },
      error: () => {
        this.isLoading.set(false);
        this.loadFailed.set(true);
      },
    });
  }
}
