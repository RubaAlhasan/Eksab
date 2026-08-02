using System;
using Volo.Abp.Application.Dtos;

namespace Eksabli.Engagement;

public class AchievementAwardDto : AuditedEntityDto<Guid>
{
    public Guid MembershipId { get; set; }

    public Guid AchievementId { get; set; }

    public DateTime AwardedAt { get; set; }
}
