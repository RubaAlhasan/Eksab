using System;
using System.ComponentModel.DataAnnotations;

namespace Eksabli.Notifications;

// Input for the admin-triggered send endpoint (IUserNotificationAppService.SendAsync) — lets platform
// staff push an announcement, and gives the rest of the application a concrete example of calling
// INotificationPublisher. Most real notifications are raised by application code calling
// INotificationPublisher directly (subscription events, audit alerts, ...), not through this endpoint.
public class SendUserNotificationDto
{
    [Required]
    public NotificationTargetType TargetType { get; set; }

    // Required when TargetType == User.
    public Guid? UserId { get; set; }

    // Required when TargetType is User (that user's own tenant, null for a Host-realm user) or Tenant.
    // Ignored for Broadcast.
    public Guid? TenantId { get; set; }

    [Required]
    public UserNotificationType Type { get; set; }

    [Required]
    [StringLength(UserNotificationConsts.MaxTitleLength)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [StringLength(UserNotificationConsts.MaxMessageLength)]
    public string Message { get; set; } = string.Empty;

    [StringLength(UserNotificationConsts.MaxCategoryLength)]
    public string? Category { get; set; }
}
