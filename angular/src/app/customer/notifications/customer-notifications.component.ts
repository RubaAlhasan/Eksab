import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { Router } from '@angular/router';
import { LocalizationPipe } from '@abp/ng.core';
import { UserNotificationsService } from '../../proxy/controllers/user-notifications.service';
import type { UserNotificationDto } from '../../proxy/user-notifications/models';
import { UserNotificationType } from '../../proxy/user-notifications/user-notification-type.enum';
import { NotificationHubService } from '../../shared/services/notification-hub.service';
import { SkeletonListComponent } from '../../shared/components/skeleton-list/skeleton-list.component';
import { EmptyStateComponent } from '../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../shared/components/error-state/error-state.component';
import { PaginationComponent } from '../../shared/components/pagination/pagination.component';

// Where a notification leads, or null when it has nothing to open. The backend names the event in `category` and
// carries the business (or, for a support reply, the ticket) in `data` as JSON (see the publishers in the backend
// notification code).
function destinationFor(item: UserNotificationDto): string | null {
  const payload = dataPayload(item.data);
  switch (item.category) {
    case 'points.earned':
      return payload.tenantId ? `/customer/wallet/${payload.tenantId}` : null;
    case 'reward.redeemed':
    case 'reward.expired':
      return '/customer/coupons';
    case 'smartdeal.collected':
      return '/customer/smart-deals/orders';
    case 'smartdeal.price_drop':
      return '/customer/smart-deals';
    case 'support.replied':
      return payload.ticketId ? `/customer/support/${payload.ticketId}` : '/customer/support';
    default:
      return null;
  }
}

function dataPayload(data: string | null | undefined): { tenantId?: string; ticketId?: string } {
  if (!data) return {};
  try {
    return JSON.parse(data) as { tenantId?: string; ticketId?: string };
  } catch {
    return {};
  }
}

const PAGE_SIZE = 15;
// Messages longer than this get a three-line preview and a "Show more" hint. Shorter ones are shown whole.
const LONG_MESSAGE_LENGTH = 140;
type FilterTab = 'all' | 'unread';

/**
 * "Alerts" tab — a full, paged inbox (`UserNotificationsService.getList`), distinct from
 * `NotificationHubService.recentNotifications` (which is a capped-at-30, dropdown-shaped cache for the
 * staff-portal bell icon this app doesn't use here). Mark-as-read/mark-all-as-read still go through
 * `NotificationHubService` so the bottom-nav unread badge (also backed by that same service, see
 * `CustomerLayoutComponent`) stays in sync with what happens on this page — this page additionally
 * mirrors the change into its own locally-fetched page of rows, which the hub's own cache doesn't cover.
 */
@Component({
  selector: 'app-customer-notifications',
  templateUrl: './customer-notifications.component.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, LocalizationPipe, SkeletonListComponent, EmptyStateComponent, ErrorStateComponent, PaginationComponent],
})
export class CustomerNotificationsComponent implements OnInit {
  private readonly userNotificationsService = inject(UserNotificationsService);
  protected readonly hub = inject(NotificationHubService);
  private readonly router = inject(Router);

  protected readonly Type = UserNotificationType;
  // Ids of the long notifications the member has opened to read in full.
  protected readonly expandedIds = signal<ReadonlySet<string>>(new Set());
  protected readonly filter = signal<FilterTab>('all');
  protected readonly notifications = signal<UserNotificationDto[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly isLoading = signal(true);
  protected readonly loadFailed = signal(false);
  protected readonly pageIndex = signal(0);
  protected readonly totalPages = computed(() => Math.max(1, Math.ceil(this.totalCount() / PAGE_SIZE)));

  ngOnInit(): void {
    this.hub.connect();
    this.load();
  }

  protected retry(): void {
    this.load();
  }

  protected selectFilter(filter: FilterTab): void {
    if (this.filter() === filter) return;
    this.filter.set(filter);
    this.pageIndex.set(0);
    this.load();
  }

  protected goToPage(index: number): void {
    if (index < 0 || index >= this.totalPages()) return;
    this.pageIndex.set(index);
    this.load();
  }

  protected onItemClick(item: UserNotificationDto): void {
    if (!item.isRead) {
      this.hub.markAsRead(item.id!);
      this.notifications.update(list =>
        list.map(n => (n.id === item.id ? { ...n, isRead: true, readAt: new Date().toISOString() } : n)),
      );
    }

    // A notification that leads somewhere opens it; any other one just expands its own text.
    const destination = destinationFor(item);
    if (destination) {
      void this.router.navigateByUrl(destination);
      return;
    }
    this.toggleExpanded(item.id);
  }

  protected isLongMessage(message: string | null | undefined): boolean {
    return (message?.length ?? 0) > LONG_MESSAGE_LENGTH;
  }

  protected isExpanded(id: string | null | undefined): boolean {
    return !!id && this.expandedIds().has(id);
  }

  protected toggleExpanded(id: string | null | undefined): void {
    if (!id) return;
    this.expandedIds.update(ids => {
      const next = new Set(ids);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }

  protected markAllAsRead(): void {
    this.hub.markAllAsRead();
    this.notifications.update(list => list.map(n => ({ ...n, isRead: true, readAt: new Date().toISOString() })));
  }

  protected iconFor(type: UserNotificationType): string {
    switch (type) {
      case UserNotificationType.Success:
        return 'fa-circle-check text-success';
      case UserNotificationType.Warning:
        return 'fa-triangle-exclamation text-warning';
      case UserNotificationType.Error:
        return 'fa-circle-exclamation text-danger';
      default:
        return 'fa-circle-info text-info';
    }
  }

  private load(): void {
    this.isLoading.set(true);
    this.loadFailed.set(false);
    this.userNotificationsService
      .getList({
        isRead: this.filter() === 'unread' ? false : null,
        skipCount: this.pageIndex() * PAGE_SIZE,
        maxResultCount: PAGE_SIZE,
      })
      .subscribe({
        next: result => {
          this.notifications.set(result.items ?? []);
          this.totalCount.set(result.totalCount ?? 0);
          this.isLoading.set(false);
        },
        error: () => {
          this.isLoading.set(false);
          this.loadFailed.set(true);
        },
      });
  }
}
