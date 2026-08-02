using System;
using Volo.Abp.Application.Dtos;

namespace Eksabli.Engagement;

public class ReferralDto : AuditedEntityDto<Guid>
{
    public Guid ReferrerMembershipId { get; set; }

    public Guid RefereeCustomerId { get; set; }

    public Guid? TenantId { get; set; }

    public ReferralStatus Status { get; set; }
}
