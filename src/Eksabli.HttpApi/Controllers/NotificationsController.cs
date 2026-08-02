using System.Threading.Tasks;
using Eksabli.Notifications;
using Eksabli.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;

namespace Eksabli.Controllers;

[ApiController]
[Route("api/app/notification")]
[Authorize(EksabliPermissions.Notifications.Send)]
public class NotificationsController : EksabliController
{
    private readonly INotificationAppService _notificationAppService;

    public NotificationsController(INotificationAppService notificationAppService)
    {
        _notificationAppService = notificationAppService;
    }

    [HttpPost]
    public Task<NotificationDto> SendAsync(SendNotificationDto input)
    {
        return _notificationAppService.SendAsync(input);
    }

    [HttpGet]
    public Task<PagedResultDto<NotificationDto>> GetListAsync([FromQuery] NotificationListFilterDto input)
    {
        return _notificationAppService.GetListAsync(input);
    }

    [HttpGet("quota-usage")]
    public Task<NotificationQuotaUsageDto> GetQuotaUsageAsync()
    {
        return _notificationAppService.GetQuotaUsageAsync();
    }
}
