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
}
