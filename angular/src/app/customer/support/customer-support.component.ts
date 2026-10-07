import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { SupportTicketsService } from '../../proxy/controllers/support-tickets.service';
import type { SupportTicketDto } from '../../proxy/platform/models';
import { SupportTicketStatus } from '../../proxy/platform/support-ticket-status.enum';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../shared/components/error-state/error-state.component';
import { SkeletonListComponent } from '../../shared/components/skeleton-list/skeleton-list.component';
import { StatusBadgeComponent, StatusBadgeVariant } from '../../shared/components/status-badge/status-badge.component';

/**
 * The customer's own help requests. The backend already scopes the list to the caller (SupportTicketAppService.GetListAsync),
 * so this page asks for everything and gets only the customer's own tickets.
 */
@Component({
  selector: 'app-customer-support',
  templateUrl: './customer-support.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink,
    DatePipe,
    LocalizationPipe,
    EmptyStateComponent,
    ErrorStateComponent,
    SkeletonListComponent,
    StatusBadgeComponent,
  ],
})
export class CustomerSupportComponent implements OnInit {
  private readonly supportTicketsService = inject(SupportTicketsService);

  protected readonly tickets = signal<SupportTicketDto[]>([]);
  protected readonly isLoading = signal(true);
  protected readonly loadFailed = signal(false);

  ngOnInit(): void {
    this.load();
  }

  protected retry(): void {
    this.load();
  }

  protected statusLabelKey(status: SupportTicketStatus | undefined): string {
    switch (status) {
      case SupportTicketStatus.InProgress:
        return '::Wallet:Support:StatusInProgress';
      case SupportTicketStatus.Resolved:
        return '::Wallet:Support:StatusResolved';
      case SupportTicketStatus.Closed:
        return '::Wallet:Support:StatusClosed';
      default:
        return '::Wallet:Support:StatusOpen';
    }
  }

  protected statusVariant(status: SupportTicketStatus | undefined): StatusBadgeVariant {
    switch (status) {
      case SupportTicketStatus.InProgress:
        return 'info';
      case SupportTicketStatus.Resolved:
        return 'success';
      case SupportTicketStatus.Closed:
        return 'neutral';
      default:
        return 'warning';
    }
  }

  private load(): void {
    this.isLoading.set(true);
    this.loadFailed.set(false);
    this.supportTicketsService.getList({ sorting: 'creationTime desc', skipCount: 0, maxResultCount: 50 }).subscribe({
      next: result => {
        this.tickets.set(result.items ?? []);
        this.isLoading.set(false);
      },
      error: () => {
        this.isLoading.set(false);
        this.loadFailed.set(true);
      },
    });
  }
}
