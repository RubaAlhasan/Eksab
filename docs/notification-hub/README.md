# Notification Hub

A centralized, real-time notification system for Eksabli, built on ASP.NET Core SignalR (web) and
Firebase Cloud Messaging (mobile), sitting behind one abstraction application code calls without
knowing which transport(s) a given notification goes out over.

```
Application Event
      │  (INotificationPublisher.PublishToUser/Tenant/AllAsync)
      ▼
NotificationPublisher (Eksabli.Domain/Notifications)
      │
      ├── Persists NotificationMessage + UserNotification row(s)   → history, read/unread, offline delivery
      ├── IRealTimeNotifier  → SignalRRealTimeNotifier  → NotificationHub → Angular Web
      └── IPushNotificationSender → FirebaseCloudMessagingSender → FCM  → Flutter Mobile
```

This is a distinct system from the existing `Notification`/`INotificationSender` (campaign delivery
log — see `docs/eksabli-loyalty-platform/features/05-campaigns-notifications/README.md`), which is
customer/`Membership`-scoped and driven by Campaigns. The Notification Hub targets `IdentityUser`/
`TenantId` directly, so it covers **any** authenticated user of the system — business/platform staff on
the Angular portals and customers on the Flutter app alike — for system/application events (subscription
past-due, audit alerts, admin broadcasts, ...), not marketing campaigns.

## Why two storage entities

`NotificationMessage` (the content) and `UserNotification` (one row per actual recipient) are split the
same way ABP Commercial's own Notification System splits them: a Tenant/Broadcast notification is read
by many different people, and per-recipient read/unread state can't live on a single shared row without
one person's "read" marking it read for everyone. `UserNotification` rows are fanned out **at publish
time** (`NotificationPublisher.PublishToTenantAsync`/`PublishToAllAsync` enumerate the target's current
users and insert one row each) — so the read path is a plain `Where(x => x.UserId == me)`, no
cross-tenant query tricks needed once a row exists.

The trade-off: a Tenant/Broadcast notification with many recipients means many rows and (for a future
FCM fan-out to more than the "one direct user" case) many push calls — both of those are intentionally
**not** done inline for Tenant/Broadcast today, same reasoning as the existing
`NotificationConsts.MaxDailyNotificationsPerTenant` quota on the campaign channel. Online recipients
still get the SignalR push instantly (it doesn't require the fan-out rows, only a connection to the
tenant's group); offline recipients see it next time they open the feed.

## Targeting

| Target | UserId | TenantId | Delivery |
|---|---|---|---|
| **User** | set | that user's own tenant (null if Host-realm) | `Clients.User(userId)` — SignalR's own user-mapping, `AbpSignalRUserIdProvider` (from `Volo.Abp.AspNetCore.SignalR`) already maps every connection to `ICurrentUser.Id`, no custom group needed. Also the only target that does an inline FCM push (to that user's registered `Device` rows). |
| **Tenant** | null | set | `Clients.Group("tenant:{tenantId}")` — `NotificationHub` joins every connection to its tenant's group in `OnConnectedAsync`/leaves in `OnDisconnectedAsync`. |
| **Broadcast** | null | null | `Clients.All`. |

## Backend pieces

| Piece | Location |
|---|---|
| `UserNotificationType`, `NotificationTargetType`, `UserNotificationConsts` | `Eksabli.Domain.Shared/Notifications/` |
| `NotificationMessage`, `UserNotification` entities, `IUserNotificationRepository` | `Eksabli.Domain/Notifications/` |
| `INotificationPublisher` / `NotificationPublisher` — the "app code doesn't depend on SignalR" seam | `Eksabli.Domain/Notifications/NotificationPublisher.cs` |
| `IRealTimeNotifier` (abstraction) / `NullRealTimeNotifier` (default) | `Eksabli.Domain/Notifications/` |
| `SignalRRealTimeNotifier`, `NotificationHub`, `INotificationHubClient` — the real transport | `Eksabli.HttpApi.Host/Notifications/` (swapped in over the Null implementation in `EksabliHttpApiHostModule.ConfigureServices`) |
| `FirebaseCloudMessagingSender` — real `IPushNotificationSender` | `Eksabli.Domain/Notifications/FirebaseCloudMessagingSender.cs` (swapped in over `NullPushNotificationSender` once `Fcm:CredentialsFilePath` is configured — see `EksabliDomainModule.ConfigureFcm`) |
| `IUserNotificationAppService` / `UserNotificationAppService` — self-service feed + admin send | `Eksabli.Application(.Contracts)/Notifications/` |
| `UserNotificationsController` (`/api/app/user-notifications`) | `Eksabli.HttpApi/Controllers/` |
| `EfCoreUserNotificationRepository`, migration `Added_NotificationHub` | `Eksabli.EntityFrameworkCore/` |

### Calling it from application code

```csharp
public class SomeAppService : ApplicationService
{
    private readonly INotificationPublisher _notificationPublisher;
    // ...

    public async Task DoSomethingAsync()
    {
        // ... business logic ...

        await _notificationPublisher.PublishToUserAsync(
            ownerUserId, CurrentTenant.Id, UserNotificationType.Warning,
            "Payment failed", "Your subscription payment could not be processed.",
            category: "billing.payment_failed",
            data: new { subscriptionId });
    }
}
```

`PublishToTenantAsync(tenantId, ...)` and `PublishToAllAsync(...)` follow the same shape. Persistence
always happens; the real-time push and FCM push are best-effort (failures are logged, never thrown —
see `NotificationPublisher.TryAsync`).

### REST API (`/api/app/user-notifications`)

| Method | Route | Auth | Purpose |
|---|---|---|---|
| GET | `/` | authenticated | Paged feed (`?isRead=` filter) |
| GET | `/unread-count` | authenticated | Badge count |
| POST | `/{id}/mark-as-read` | authenticated, own notification only | |
| POST | `/mark-all-as-read` | authenticated | |
| POST | `/` | `Eksabli.Notifications.Broadcast` | Manual/admin trigger — see `SendUserNotificationDto` |

### Configuration

```jsonc
// appsettings.json (never commit the credentials file itself)
"Fcm": {
  "CredentialsFilePath": "/path/to/firebase-service-account.json"
}
```

Unset in dev by default — `IPushNotificationSender` stays on `NullPushNotificationSender` (logs instead
of calling Firebase) until this is configured, same posture as every other not-yet-provisioned
integration in this codebase (`NullSmsSender`, `NullPaymentGateway`).

### SignalR connection details

- Route: `/signalr-hubs/notifications` (`NotificationHub`, `[HubRoute(...)]`).
- Requires authentication (`[Authorize]` on the hub) — the same bearer token as REST calls.
- Browsers can't set an `Authorization` header on a WebSocket handshake, so the SignalR JS client sends
  the token as an `access_token` query-string parameter instead; `EksabliHttpApiHostModule` rewrites
  that back into the `Authorization` header (only for requests under `/signalr-hubs`) before
  `UseAuthentication()` runs — see `UseNotificationHubQueryStringAuthentication` in
  `Notifications/NotificationHubApplicationBuilderExtensions.cs`.
- Client event: `receiveNotification`, payload shape = `RealTimeNotificationPayload` (`id`, `type`,
  `title`, `message`, `category`, `data`, `creationTime`).

## Angular integration

- Proxy: `angular/src/app/proxy/user-notifications/` + `proxy/controllers/user-notifications.service.ts`.
- `NotificationHubService` (`angular/src/app/shared/services/notification-hub.service.ts`) — root-provided
  singleton wrapping `@microsoft/signalr`'s `HubConnection`. `connect()` is idempotent (safe to call from
  multiple components), uses `withAutomaticReconnect()`, and exposes `connectionState`/`unreadCount`/
  `recentNotifications` as signals. Falls back to a 5s retry loop if the very first connection attempt
  fails (`withAutomaticReconnect` only covers drops *after* a successful start).
- `NotificationBellComponent` (`angular/src/app/shared/components/notification-bell/`) — the bell +
  dropdown UI, dropped into both `AdminLayoutComponent` and `BusinessLayoutComponent`'s topbar (same
  component/service for both portals, since both are IdentityUsers the hub already targets the same way).

## Flutter integration

See [`flutter-fcm-integration.md`](./flutter-fcm-integration.md). Summary: Flutter never connects to
SignalR — it registers its FCM token with the existing `POST /api/app/devices` endpoint (already built
for the campaign push channel, reused as-is — no new endpoint), and `NotificationPublisher`/
`NotificationSender` push to that token through `IPushNotificationSender` →
`FirebaseCloudMessagingSender`.
