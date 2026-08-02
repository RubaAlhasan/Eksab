using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Eksabli.Notifications;

// Exposed via an explicit controller (src/Eksabli.HttpApi/Controllers/NotificationsController.cs).
[RemoteService(IsEnabled = false)]
public interface INotificationAppService : IApplicationService
{
    Task<NotificationDto> SendAsync(SendNotificationDto input);

    Task<PagedResultDto<NotificationDto>> GetListAsync(NotificationListFilterDto input);

    Task<NotificationQuotaUsageDto> GetQuotaUsageAsync();
}
