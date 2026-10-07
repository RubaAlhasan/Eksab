using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace Eksabli.Notifications;

// Exposed via CustomerNotificationPreferencesController. Only the signed-in customer's own switches are read or changed.
[RemoteService(IsEnabled = false)]
public interface ICustomerNotificationPreferenceAppService : IApplicationService
{
    Task<NotificationPreferencesDto> GetMyAsync();

    Task<NotificationPreferencesDto> UpdateMyAsync(NotificationPreferencesDto input);
}
