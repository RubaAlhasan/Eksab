using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Eksabli.Notifications;

// The real IRealTimeNotifier — wired in over NullRealTimeNotifier by EksabliHttpApiHostModule
// (ConfigureServices). NotificationPublisher (Domain) is the only caller; this class is the only place
// in the whole solution that imports Microsoft.AspNetCore.SignalR; for notifications.
public class SignalRRealTimeNotifier : IRealTimeNotifier
{
    private readonly IHubContext<NotificationHub, INotificationHubClient> _hubContext;

    public ILogger<SignalRRealTimeNotifier> Logger { get; set; } = NullLogger<SignalRRealTimeNotifier>.Instance;

    public SignalRRealTimeNotifier(IHubContext<NotificationHub, INotificationHubClient> hubContext)
    {
        _hubContext = hubContext;
    }

    public Task NotifyUserAsync(Guid userId, RealTimeNotificationPayload payload, CancellationToken cancellationToken = default)
    {
        // Matches AbpSignalRUserIdProvider.GetUserId (ICurrentUser.Id.ToString()) — every authenticated
        // connection for this user, across every open tab/device, receives it.
        return _hubContext.Clients.User(userId.ToString()).ReceiveNotification(payload);
    }

    public Task NotifyTenantAsync(Guid tenantId, RealTimeNotificationPayload payload, CancellationToken cancellationToken = default)
    {
        return _hubContext.Clients.Group(NotificationHub.TenantGroupName(tenantId)).ReceiveNotification(payload);
    }

    public Task NotifyAllAsync(RealTimeNotificationPayload payload, CancellationToken cancellationToken = default)
    {
        return _hubContext.Clients.All.ReceiveNotification(payload);
    }
}
