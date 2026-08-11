import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { LocalizationPipe } from '@abp/ng.core';
import { NotificationHubService } from '../../services/notification-hub.service';
import type { UserNotificationDto } from '../../../proxy/user-notifications/models';
import { UserNotificationType } from '../../../proxy/user-notifications/user-notification-type.enum';

/**
 * The Angular half of the real-time Notification Hub — bell icon + dropdown feed, dropped into both
 * AdminLayoutComponent and BusinessLayoutComponent's topbar (same component, same NotificationHubService
 * singleton underneath, since both portals' users are IdentityUsers the backend hub already targets by
 * UserId/TenantId). Connects on construction; NotificationHubService.connect() is idempotent so this is
 * safe even though both layouts never render at once.
 *
 * Styling (notification-bell.component.scss) deliberately does NOT lean on Bootstrap's
 * `.dropdown-menu`/`--bs-*` tokens — this app's shells define their own `--eks-*` custom properties
 * (see admin-layout.component.scss/business-layout.component.scss) with real light/dark values, and
 * those are what every other panel in the app is actually themed with. Building the panel entirely out
 * of `--eks-*` tokens means it inherits the correct palette from whichever shell it's rendered inside
 * for free (CSS custom properties cascade through the DOM regardless of component boundaries) and gets
 * dark mode for free the same way.
 */
@Component({
  selector: 'app-notification-bell',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [DatePipe, LocalizationPipe],
  templateUrl: './notification-bell.component.html',
  styleUrls: ['./notification-bell.component.scss'],
})
export class NotificationBellComponent {
  protected readonly hub = inject(NotificationHubService);

  protected readonly open = signal(false);
  protected readonly unreadBadge = computed(() => (this.hub.unreadCount() > 9 ? '9+' : String(this.hub.unreadCount())));

  constructor() {
    this.hub.connect();
  }

  protected toggle(): void {
    this.open.update((isOpen) => !isOpen);
  }

  protected markAllAsRead(): void {
    this.hub.markAllAsRead();
  }

  protected onItemClick(item: UserNotificationDto): void {
    this.hub.markAsRead(item.id);
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
}
