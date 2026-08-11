using System.Threading.Tasks;

namespace Eksabli.Notifications;

// Strongly-typed SignalR client contract for NotificationHub — what the Angular client's
// hubConnection.on("receiveNotification", ...) subscribes to. Keep this the single source of truth for
// the method name/shape on both ends.
public interface INotificationHubClient
{
    Task ReceiveNotification(RealTimeNotificationPayload payload);
}
