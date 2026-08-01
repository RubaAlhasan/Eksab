using System;
using Volo.Abp.Application.Dtos;

namespace Eksabli.Memberships;

public class MembershipDto : AuditedEntityDto<Guid>
{
    public Guid CustomerId { get; set; }

    public Guid? TenantId { get; set; }

    public DateTime JoinedAt { get; set; }

    public MembershipStatus Status { get; set; }
}
