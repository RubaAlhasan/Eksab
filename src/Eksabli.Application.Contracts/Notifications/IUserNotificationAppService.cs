using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Eksabli.Notifications;

// Self-service history/read-state for the current user's own notification feed, plus one
// permission-gated send/broadcast action for platform staff. Exposed via an explicit controller
// (src/Eksabli.HttpApi/Controllers/UserNotificationsController.cs), same treatment as INotificationAppService.
[RemoteService(IsEnabled = false)]
public interface IUserNotificationAppService : IApplicationService
{
    Task<PagedResultDto<UserNotificationDto>> GetListAsync(UserNotificationListFilterDto input);

    Task<int> GetUnreadCountAsync();

    Task MarkAsReadAsync(Guid id);

    Task MarkAllAsReadAsync();

    Task SendAsync(SendUserNotificationDto input);
}
