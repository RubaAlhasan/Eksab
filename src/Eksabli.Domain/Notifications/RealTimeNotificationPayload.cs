using System;

namespace Eksabli.Notifications;

// What NotificationPublisher hands to IRealTimeNotifier for the live SignalR push — deliberately a
// plain Domain-layer class (not the SignalR client's own message type) so the Domain/Application layers
// never take a compile-time dependency on SignalR. The concrete IRealTimeNotifier implementation
// (HttpApi.Host) is the only place that knows this shape gets serialized straight down the wire.
public class RealTimeNotificationPayload
{
    public Guid Id { get; init; } // UserNotification.Id when known (User target); message Id otherwise
    public UserNotificationType Type { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string? Category { get; init; }
    public string? Data { get; init; }
    public DateTime CreationTime { get; init; }
}
