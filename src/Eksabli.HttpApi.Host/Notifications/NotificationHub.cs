using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Volo.Abp.AspNetCore.SignalR;

namespace Eksabli.Notifications;

// The SignalR transport for the Notification Hub — Angular Web only (Flutter uses FCM, never this hub;
// see docs/notification-hub.md). Requires authentication (ABP's own bearer-token pipeline handles this
// hub connection exactly like any HttpApi request — see the access_token->Authorization-header
// middleware in EksabliHttpApiHostModule for why that's needed on top of the usual header for browser
// WebSocket clients). Individual-user delivery needs nothing beyond that: AbpSignalRUserIdProvider
// (from Volo.Abp.AspNetCore.SignalR) already maps every connection to ICurrentUser.Id, so
// Clients.User(userId) just works. Tenant delivery uses one SignalR group per tenant, joined/left here.
[Authorize]
[HubRoute("/signalr-hubs/notifications")]
public class NotificationHub : AbpHub<INotificationHubClient>
{
    public override async Task OnConnectedAsync()
    {
        await JoinTenantGroupAsync();
        Logger.LogDebug("Notification hub: user {UserId} connected ({ConnectionId}).", CurrentUser.Id, Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await LeaveTenantGroupAsync();
        Logger.LogDebug("Notification hub: user {UserId} disconnected ({ConnectionId}).", CurrentUser.Id, Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    private Task JoinTenantGroupAsync()
    {
        return CurrentTenant.Id.HasValue
            ? Groups.AddToGroupAsync(Context.ConnectionId, TenantGroupName(CurrentTenant.Id.Value))
            : Task.CompletedTask;
    }

    private Task LeaveTenantGroupAsync()
    {
        // Not strictly required — SignalR drops a closed connection from every group automatically —
        // but explicit is cheap and makes the lifecycle easy to reason about/log.
        return CurrentTenant.Id.HasValue
            ? Groups.RemoveFromGroupAsync(Context.ConnectionId, TenantGroupName(CurrentTenant.Id.Value))
            : Task.CompletedTask;
    }

    internal static string TenantGroupName(Guid tenantId) => $"tenant:{tenantId}";
}
