using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace Eksabli.Engagement;

// A customer following a business pre-membership — deliberately serves both the "favorite" (customer
// UI concept) and "follow" (business marketing-target concept) needs with one row. See
// docs/eksabli-loyalty-platform/03-database-design.md#engagement--gamification.
public class Follow : AuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid CustomerId { get; private set; }

    public Guid? TenantId { get; private set; }

    public DateTime FollowedAt { get; private set; }

    protected Follow()
    {
        /* Required by the ORM */
    }

    private Follow(Guid id, Guid customerId, DateTime followedAt)
        : base(id)
    {
        CustomerId = customerId;
        FollowedAt = followedAt;
    }

    public static Follow Create(Guid id, Guid customerId, DateTime followedAt)
    {
        return new Follow(id, customerId, followedAt);
    }
}
