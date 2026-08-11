using System;
using Volo.Abp.Application.Dtos;

namespace Eksabli.Notifications;

public class UserNotificationDto : EntityDto<Guid>
{
    public UserNotificationType Type { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string? Category { get; set; }

    public string? Data { get; set; }

    public bool IsRead { get; set; }

    public DateTime? ReadAt { get; set; }

    public DateTime CreationTime { get; set; }
}
