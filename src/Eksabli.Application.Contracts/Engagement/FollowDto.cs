using System;
using Volo.Abp.Application.Dtos;

namespace Eksabli.Engagement;

public class FollowDto : AuditedEntityDto<Guid>
{
    public Guid CustomerId { get; set; }

    public Guid? TenantId { get; set; }

    public DateTime FollowedAt { get; set; }
}
