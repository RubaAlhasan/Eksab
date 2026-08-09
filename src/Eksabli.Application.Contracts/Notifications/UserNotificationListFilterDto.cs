using Volo.Abp.Application.Dtos;

namespace Eksabli.Notifications;

public class UserNotificationListFilterDto : PagedResultRequestDto
{
    public bool? IsRead { get; set; }
}
