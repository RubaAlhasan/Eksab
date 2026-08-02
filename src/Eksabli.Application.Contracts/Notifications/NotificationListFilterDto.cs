using System;
using Volo.Abp.Application.Dtos;

namespace Eksabli.Notifications;

public class NotificationListFilterDto : PagedAndSortedResultRequestDto
{
    public Guid? CampaignId { get; set; }

    public NotificationStatus? Status { get; set; }

    public NotificationChannel? Channel { get; set; }
}
