using System.Threading;
using System.Threading.Tasks;

namespace Eksabli.Notifications;

// Routes a Notification to its channel's adapter (push/email/SMS) and marks it Sent/Failed. Invoked
// only from NotificationDispatchJob (the background job queue), never called synchronously from an
// app service — see docs/eksabli-loyalty-platform/features/05-campaigns-notifications/README.md.
public interface INotificationSender
{
    Task SendAsync(Notification notification, CancellationToken cancellationToken = default);
}
