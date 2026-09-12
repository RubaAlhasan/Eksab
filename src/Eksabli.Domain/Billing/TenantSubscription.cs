using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace Eksabli.Billing;

// TenantId doubles as both "which ABP tenant this bills" and the IMultiTenant discriminator — a
// tenant Owner's ambient-tenant query and a platform admin's Disable<IMultiTenant>() query both work
// with zero extra plumbing, same as Membership.
public class TenantSubscription : AuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }

    public Guid PlanId { get; private set; }

    public DateTime StartDate { get; private set; }

    public DateTime RenewalDate { get; private set; }

    public TenantSubscriptionStatus Status { get; private set; }

    // A tenant Owner's ChangePlanAsync no longer applies immediately — it records a request here, and
    // only a platform admin's ApprovePlanChangeAsync (AdminSubscriptionAppService) actually reassigns
    // PlanId and pushes the new plan's feature limits. PlanId itself stays whatever plan is currently
    // active/billed until that approval happens.
    public Guid? PendingPlanId { get; private set; }

    public DateTime? PlanChangeRequestedAt { get; private set; }

    protected TenantSubscription()
    {
    }

    private TenantSubscription(Guid id, Guid planId, DateTime startDate, DateTime renewalDate, TenantSubscriptionStatus status)
        : base(id)
    {
        PlanId = planId;
        StartDate = startDate;
        RenewalDate = renewalDate;
        Status = status;
    }

    public static TenantSubscription Create(Guid id, Guid planId, DateTime startDate, DateTime renewalDate, TenantSubscriptionStatus status)
    {
        return new TenantSubscription(id, planId, startDate, renewalDate, status);
    }

    public void ChangePlan(Guid planId) => PlanId = planId;

    public void Renew(DateTime newRenewalDate) => RenewalDate = newRenewalDate;

    public void MarkActive() => Status = TenantSubscriptionStatus.Active;

    public void MarkPastDue() => Status = TenantSubscriptionStatus.PastDue;

    public void Cancel() => Status = TenantSubscriptionStatus.Cancelled;

    // Requested by the tenant Owner (BillingAppService.ChangePlanAsync) — does NOT touch PlanId. A
    // second request before the first is resolved simply overwrites it (last request wins); there's
    // only ever one pending change at a time, never a queue.
    public void RequestPlanChange(Guid planId, DateTime requestedAt)
    {
        PendingPlanId = planId;
        PlanChangeRequestedAt = requestedAt;
    }

    // Platform admin accepted the request (AdminSubscriptionAppService.ApprovePlanChangeAsync) — this
    // is the one place PlanId actually changes as a result of a tenant-initiated request. Also marks
    // the subscription Active: accepting a plan change is the platform's confirmation that the tenant
    // is in good standing on it, the same real-world moment a Trialing-or-PastDue subscription becomes
    // Active. The caller is responsible for checking PendingPlanId.HasValue first — this method doesn't
    // guard against "nothing pending" itself, same "dumb domain method, validating caller" shape as
    // ChangePlan/Renew above.
    public void ApprovePendingPlanChange()
    {
        ChangePlan(PendingPlanId!.Value);
        PendingPlanId = null;
        PlanChangeRequestedAt = null;
        Status = TenantSubscriptionStatus.Active;
    }

    // Platform admin declined the request — PlanId is untouched, Status is untouched (whatever it was
    // before the request stays, e.g. still Trialing).
    public void RejectPendingPlanChange()
    {
        PendingPlanId = null;
        PlanChangeRequestedAt = null;
    }
}
