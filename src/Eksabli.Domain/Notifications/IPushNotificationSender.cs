using System.Threading.Tasks;

namespace Eksabli.Notifications;

public interface IPushNotificationSender
{
    Task SendAsync(string pushToken, string title, string body);
}
