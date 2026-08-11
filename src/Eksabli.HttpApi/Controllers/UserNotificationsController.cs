using System;
using System.Threading.Tasks;
using Eksabli.Notifications;
using Eksabli.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;

namespace Eksabli.Controllers;

// The REST half of the Notification Hub — GetList/GetUnreadCount/MarkAsRead/MarkAllAsRead are
// self-service (any authenticated user, own feed only, enforced in UserNotificationAppService). Send is
// the one action that needs Notifications.Broadcast, gated here rather than on the app service — same
// split as NotificationsController.
[ApiController]
[Route("api/app/user-notifications")]
[Authorize]
public class UserNotificationsController : EksabliController
{
    private readonly IUserNotificationAppService _userNotificationAppService;

    public UserNotificationsController(IUserNotificationAppService userNotificationAppService)
    {
        _userNotificationAppService = userNotificationAppService;
    }

    [HttpGet]
    public Task<PagedResultDto<UserNotificationDto>> GetListAsync([FromQuery] UserNotificationListFilterDto input)
    {
        return _userNotificationAppService.GetListAsync(input);
    }

    [HttpGet("unread-count")]
    public Task<int> GetUnreadCountAsync()
    {
        return _userNotificationAppService.GetUnreadCountAsync();
    }

    [HttpPost("{id}/mark-as-read")]
    public Task MarkAsReadAsync(Guid id)
    {
        return _userNotificationAppService.MarkAsReadAsync(id);
    }

    [HttpPost("mark-all-as-read")]
    public Task MarkAllAsReadAsync()
    {
        return _userNotificationAppService.MarkAllAsReadAsync();
    }

    [HttpPost]
    [Authorize(EksabliPermissions.Notifications.Broadcast)]
    public Task SendAsync(SendUserNotificationDto input)
    {
        return _userNotificationAppService.SendAsync(input);
    }
}
