using System;
using System.Threading;
using System.Threading.Tasks;

namespace Eksabli.Notifications;

// The single entry point application code should call to raise a notification — "Application Event ->
// Notification Service -> SignalR (web) + FCM (mobile)" from the architecture brief. Callers never talk
// to IRealTimeNotifier/IPushNotificationSender/the repositories directly; this is what keeps a business
// event (e.g. "subscription past due", "audit alert") from having to know two delivery mechanisms exist.
public interface INotificationPublisher
{
    /// <summary>
    /// Notifies one specific user. <paramref name="tenantId"/> is that user's own tenant (null for a
    /// Host-realm user) — pass it explicitly rather than relying on ambient ICurrentTenant, since the
    /// publisher is commonly called from background jobs/workers with no request-scoped tenant context.
    /// </summary>
    Task PublishToUserAsync(
        Guid userId, Guid? tenantId, UserNotificationType type, string title, string message,
        string? category = null, object? data = null, CancellationToken cancellationToken = default);

    /// <summary>Notifies every current user of one tenant (business).</summary>
    Task PublishToTenantAsync(
        Guid tenantId, UserNotificationType type, string title, string message,
        string? category = null, object? data = null, CancellationToken cancellationToken = default);

    /// <summary>Notifies every user, across every tenant and the Host realm.</summary>
    Task PublishToAllAsync(
        UserNotificationType type, string title, string message,
        string? category = null, object? data = null, CancellationToken cancellationToken = default);
}
