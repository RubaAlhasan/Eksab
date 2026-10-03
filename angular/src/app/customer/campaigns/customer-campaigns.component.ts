import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { CustomerCampaignService } from '../../proxy/controllers/customer-campaign.service';
import type { CustomerCampaignDto } from '../../proxy/campaigns/models';
import { SkeletonListComponent } from '../../shared/components/skeleton-list/skeleton-list.component';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../shared/components/error-state/error-state.component';
import { campaignTypeEmoji, campaignTypeLabelKey } from '../../shared/utils/campaign-display.util';

/**
 * Global campaign feed — `CustomerCampaignAppService.GetMyFeedAsync()` already filters server-side to
 * live campaigns across every joined business whose target segment includes this customer (confirmed by
 * reading its own doc comment), so no client-side date/segment filtering is needed here.
 */
@Component({
  selector: 'app-customer-campaigns',
  templateUrl: './customer-campaigns.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterLink, DatePipe, LocalizationPipe, SkeletonListComponent, EmptyStateComponent, ErrorStateComponent],
})
export class CustomerCampaignsComponent implements OnInit {
  private readonly customerCampaignService = inject(CustomerCampaignService);

  protected readonly campaigns = signal<CustomerCampaignDto[]>([]);
  protected readonly isLoading = signal(true);
  protected readonly loadFailed = signal(false);

  protected readonly campaignTypeEmoji = campaignTypeEmoji;
  protected readonly campaignTypeLabelKey = campaignTypeLabelKey;

  ngOnInit(): void {
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
