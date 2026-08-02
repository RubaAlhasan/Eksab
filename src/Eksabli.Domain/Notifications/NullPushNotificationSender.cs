using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Volo.Abp.DependencyInjection;

namespace Eksabli.Notifications;

// Placeholder push sender — no real push provider (FCM/OneSignal) has been chosen yet (open question,
// see docs/eksabli-loyalty-platform/features/05-campaigns-notifications/README.md). Just logs the
// message so the dispatch pipeline stays exercisable in dev/testing. Mirrors
// src/Eksabli.Domain/Sms/NullSmsSender.cs.
public class NullPushNotificationSender : IPushNotificationSender, ITransientDependency
{
    public ILogger<NullPushNotificationSender> Logger { get; set; } = NullLogger<NullPushNotificationSender>.Instance;

    public Task SendAsync(string pushToken, string title, string body)
    {
        Logger.LogWarning(
            "[DEV PUSH PLACEHOLDER — no real push provider configured yet] To: {PushToken} | {Title}: {Body}",
            pushToken, title, body);
        return Task.CompletedTask;
    }
}
