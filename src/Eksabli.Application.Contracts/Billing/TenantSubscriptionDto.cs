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

    // Set only while a tenant-requested plan change is awaiting a platform admin's decision
    // (AdminSubscriptionAppService.ApprovePlanChangeAsync/RejectPlanChangeAsync) — PlanId/PlanName above
    // still describe the currently active plan the whole time.
    public Guid? PendingPlanId { get; set; }

    public string? PendingPlanName { get; set; }

    public DateTime? PlanChangeRequestedAt { get; set; }
}
