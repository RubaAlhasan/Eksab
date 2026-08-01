using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace Eksabli.Memberships;

public class Membership : AuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid CustomerId { get; private set; }

    public Guid? TenantId { get; private set; }

    public DateTime JoinedAt { get; private set; }

    public MembershipStatus Status { get; private set; }

    protected Membership()
    {
        /* Required by the ORM */
    }

    private Membership(Guid id, Guid customerId, DateTime joinedAt)
        : base(id)
    {
        CustomerId = customerId;
        JoinedAt = joinedAt;
        Status = MembershipStatus.Active;

        // TenantId is intentionally NOT a constructor parameter — ABP populates it
        // automatically from ICurrentTenant.Id at insert time because this class
        // implements IMultiTenant. Setting it here would fight the framework.
    }

    public static Membership Create(Guid id, Guid customerId, DateTime joinedAt)
    {
        return new Membership(id, customerId, joinedAt);
    }

    public void Freeze()
    {
        Status = MembershipStatus.Frozen;
    }

    public void Reactivate()
    {
        Status = MembershipStatus.Active;
    }
}
