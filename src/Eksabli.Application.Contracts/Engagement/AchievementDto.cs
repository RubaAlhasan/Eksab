using System;
using Volo.Abp.Application.Dtos;

namespace Eksabli.Engagement;

public class AchievementDto : FullAuditedEntityDto<Guid>
{
    public Guid? TenantId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? CriteriaJson { get; set; }
}
