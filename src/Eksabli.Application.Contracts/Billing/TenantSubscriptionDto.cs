using System;
using Volo.Abp.Application.Dtos;

namespace Eksabli.Billing;

public class TenantSubscriptionDto : AuditedEntityDto<Guid>
{
    public Guid? TenantId { get; set; }

    public Guid PlanId { get; set; }

    public string? PlanName { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime RenewalDate { get; set; }

    public TenantSubscriptionStatus Status { get; set; }
}
