using System;
using System.ComponentModel.DataAnnotations;

namespace Eksabli.Notifications;

public class SendNotificationDto
{
    [Required]
    public Guid MembershipId { get; set; }

    [Required]
    public NotificationChannel Channel { get; set; }

    [Required]
    [StringLength(NotificationConsts.MaxTitleLength)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [StringLength(NotificationConsts.MaxBodyLength)]
    public string Body { get; set; } = string.Empty;
}
