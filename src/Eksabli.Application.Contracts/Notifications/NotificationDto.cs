using System;
using Volo.Abp.Application.Dtos;

namespace Eksabli.Notifications;

public class NotificationDto : AuditedEntityDto<Guid>
{
    public Guid? MembershipId { get; set; }

    public Guid? TenantId { get; set; }

    public Guid? CampaignId { get; set; }

    public NotificationChannel Channel { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public NotificationStatus Status { get; set; }

    public DateTime? SentAt { get; set; }
}
