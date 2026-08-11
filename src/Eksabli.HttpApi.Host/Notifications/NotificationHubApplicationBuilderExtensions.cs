using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Primitives;

namespace Eksabli.Notifications;

public static class NotificationHubApplicationBuilderExtensions
{
    // Browsers' WebSocket API can't set an Authorization header on the handshake, so the SignalR JS
    // client falls back to an access_token query-string parameter for hub connections (see the Angular
    // NotificationSignalRService's accessTokenFactory). ASP.NET Core's auth handlers (OpenIddict
    // validation here) only ever read the Authorization header, so this rewrites one into the other
    // before UseAuthentication runs — the standard fix for SignalR + bearer-token auth — scoped to just
    // the hub path so it can't be used to smuggle a token into unrelated requests.
    public static IApplicationBuilder UseNotificationHubQueryStringAuthentication(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            var accessToken = context.Request.Query["access_token"];
            if (!StringValues.IsNullOrEmpty(accessToken) && context.Request.Path.StartsWithSegments("/signalr-hubs"))
            {
                context.Request.Headers["Authorization"] = $"Bearer {accessToken}";
            }

            await next();
        });
    }
}
