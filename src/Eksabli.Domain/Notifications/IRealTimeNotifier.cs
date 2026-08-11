using System;
using System.Threading;
using System.Threading.Tasks;

namespace Eksabli.Notifications;

// The "NotificationService does not depend directly on SignalR" seam. NotificationPublisher (this
// namespace) is the only caller; the real implementation (SignalRRealTimeNotifier, wired in
// Eksabli.HttpApi.Host — see its DependsOn/ConfigureServices) pushes over the NotificationHub. Anything
// resolving this without HttpApi.Host loaded (background workers, tests) gets NullRealTimeNotifier
// instead — the push is simply skipped, the DB row from NotificationPublisher's persistence step still
// carries the notification for the recipient to see next time they open the feed.
public interface IRealTimeNotifier
{
    Task NotifyUserAsync(Guid userId, RealTimeNotificationPayload payload, CancellationToken cancellationToken = default);

    Task NotifyTenantAsync(Guid tenantId, RealTimeNotificationPayload payload, CancellationToken cancellationToken = default);

    Task NotifyAllAsync(RealTimeNotificationPayload payload, CancellationToken cancellationToken = default);
}
