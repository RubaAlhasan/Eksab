using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Volo.Abp.DependencyInjection;

namespace Eksabli.Notifications;

// Default IRealTimeNotifier — replaced by SignalRRealTimeNotifier once Eksabli.HttpApi.Host wires it up
// (see that project's module ConfigureServices). Mirrors NullPushNotificationSender/NullSmsSender.
public class NullRealTimeNotifier : IRealTimeNotifier, ITransientDependency
{
    public ILogger<NullRealTimeNotifier> Logger { get; set; } = NullLogger<NullRealTimeNotifier>.Instance;

    public Task NotifyUserAsync(Guid userId, RealTimeNotificationPayload payload, CancellationToken cancellationToken = default)
    {
        Logger.LogDebug("[No real-time transport wired] Would notify user {UserId}: {Title}", userId, payload.Title);
        return Task.CompletedTask;
    }

    public Task NotifyTenantAsync(Guid tenantId, RealTimeNotificationPayload payload, CancellationToken cancellationToken = default)
    {
        Logger.LogDebug("[No real-time transport wired] Would notify tenant {TenantId}: {Title}", tenantId, payload.Title);
        return Task.CompletedTask;
    }

    public Task NotifyAllAsync(RealTimeNotificationPayload payload, CancellationToken cancellationToken = default)
    {
        Logger.LogDebug("[No real-time transport wired] Would broadcast: {Title}", payload.Title);
        return Task.CompletedTask;
    }
}
