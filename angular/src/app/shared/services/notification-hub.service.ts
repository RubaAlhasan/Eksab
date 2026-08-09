import { Injectable, computed, inject, signal } from '@angular/core';
import { AuthService } from '@abp/ng.core';
import * as signalR from '@microsoft/signalr';
import { environment } from '../../../environments/environment';
import { UserNotificationsService } from '../../proxy/controllers/user-notifications.service';
import type { UserNotificationDto } from '../../proxy/user-notifications/models';
import { UserNotificationType } from '../../proxy/user-notifications/user-notification-type.enum';

export type NotificationHubConnectionState = 'disconnected' | 'connecting' | 'connected' | 'reconnecting';

/** Wire shape of NotificationHub's `receiveNotification` push — matches
 *  Eksabli.Notifications.RealTimeNotificationPayload on the backend field-for-field. Not itself an
 *  UserNotificationDto (no isRead/readAt — a just-arrived push is unread by definition; see
 *  toFeedItem()). */
interface RealTimeNotificationPayload {
  id: string;
  type: UserNotificationType;
  title: string;
  message: string;
  category?: string | null;
  data?: string | null;
  creationTime: string;
}

const HUB_ROUTE = '/signalr-hubs/notifications';
const MAX_KEPT_ITEMS = 30;

/**
 * Single connection to the backend's NotificationHub (Eksabli.HttpApi.Host/Notifications/NotificationHub.cs),
 * shared by both portals (admin + business — see NotificationBellComponent, dropped into both layouts).
 * Root-provided singleton: `connect()` is idempotent, so both layouts calling it on init is safe — only
 * the first call actually opens a connection.
 *
 * Auth: accessTokenFactory hands SignalR the current bearer token on every (re)connect — same token
 * AuthService/RestService already use for REST calls, no separate login. The browser WebSocket API can't
 * set an Authorization header on the handshake itself, so the token also travels once as an
 * `access_token` query-string param; the backend middleware that turns that back into a header lives in
 * EksabliHttpApiHostModule (UseNotificationHubQueryStringAuthentication) — see that file for why.
 *
 * Reconnection: withAutomaticReconnect() retries with backoff and fires onreconnecting/onreconnected/
 * onclose, which this service turns into `connectionState()` — the bell shows a small offline indicator
 * rather than silently going stale.
 */
@Injectable({ providedIn: 'root' })
export class NotificationHubService {
  private readonly authService = inject(AuthService);
  private readonly userNotificationsService = inject(UserNotificationsService);

  private hubConnection: signalR.HubConnection | null = null;
  private connectPromise: Promise<void> | null = null;

  readonly connectionState = signal<NotificationHubConnectionState>('disconnected');
  readonly unreadCount = signal(0);
  readonly recentNotifications = signal<UserNotificationDto[]>([]);
  readonly hasUnread = computed(() => this.unreadCount() > 0);
  /** True until the first GetList response arrives — lets the bell show a spinner instead of a
   *  premature "you're all caught up" empty state while the initial fetch is still in flight. */
  readonly isLoading = signal(true);

  /** Opens the connection and loads the initial feed. Safe to call from multiple components — a
   *  connection already open/opening is reused. */
  connect(): void {
    if (this.hubConnection || this.connectPromise) {
      return;
    }

    this.loadInitialFeed();

    const hubUrl = `${environment.apis.default.url}${HUB_ROUTE}`;
    this.hubConnection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl, { accessTokenFactory: () => this.authService.getAccessToken() })
      .withAutomaticReconnect()
      .build();

    this.hubConnection.on('receiveNotification', (payload: RealTimeNotificationPayload) => {
      this.onNotificationReceived(payload);
    });

    this.hubConnection.onreconnecting(() => this.connectionState.set('reconnecting'));
    this.hubConnection.onreconnected(() => this.connectionState.set('connected'));
    this.hubConnection.onclose(() => this.connectionState.set('disconnected'));

    this.connectionState.set('connecting');
    this.connectPromise = this.hubConnection
      .start()
      .then(() => this.connectionState.set('connected'))
      .catch(() => {
        // withAutomaticReconnect only covers drops *after* a successful start — an initial failure (e.g.
        // API host not reachable yet) needs its own retry, so fall back to a fixed delay and try again.
        this.connectionState.set('disconnected');
        this.connectPromise = null;
        setTimeout(() => this.connect(), 5000);
      });
  }

  disconnect(): void {
    this.connectPromise = null;
    void this.hubConnection?.stop();
    this.hubConnection = null;
    this.connectionState.set('disconnected');
  }

  markAsRead(id: string): void {
    const item = this.recentNotifications().find((n) => n.id === id);
    if (!item || item.isRead) {
      return;
    }

    this.recentNotifications.update((items) => items.map((n) => (n.id === id ? { ...n, isRead: true } : n)));
    this.unreadCount.update((count) => Math.max(0, count - 1));

    this.userNotificationsService.markAsRead(id).subscribe();
  }

  markAllAsRead(): void {
    this.recentNotifications.update((items) => items.map((n) => ({ ...n, isRead: true })));
    this.unreadCount.set(0);

    this.userNotificationsService.markAllAsRead().subscribe();
  }

  private loadInitialFeed(): void {
    this.userNotificationsService.getUnreadCount().subscribe((count) => this.unreadCount.set(count));
    this.userNotificationsService
      .getList({ maxResultCount: MAX_KEPT_ITEMS, skipCount: 0 })
      .subscribe({
        next: (result) => this.recentNotifications.set(result.items),
        complete: () => this.isLoading.set(false),
        error: () => this.isLoading.set(false),
      });
  }

  private onNotificationReceived(payload: RealTimeNotificationPayload): void {
    const item: UserNotificationDto = {
      id: payload.id,
      type: payload.type,
      title: payload.title,
      message: payload.message,
      category: payload.category,
      data: payload.data,
      isRead: false,
      readAt: null,
      creationTime: payload.creationTime,
    };

    this.recentNotifications.update((items) => [item, ...items].slice(0, MAX_KEPT_ITEMS));
    this.unreadCount.update((count) => count + 1);
  }
}
