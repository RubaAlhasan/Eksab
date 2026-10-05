using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace Eksabli.SmartOffers;

// A customer asked to be told when a deal's price is about to drop. The watch is removed once that notice goes out, so a
// watch fires once and then has nothing left to do. Scoped to the business that owns the deal (TenantId), like everything else.
public class SmartOfferWatch : CreationAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }

    // The customer's own identity id: the notice is addressed to the user, the same as every other customer notification.
    public Guid CustomerId { get; private set; }

    public Guid SmartOfferId { get; private set; }

    protected SmartOfferWatch()
    {
        /* Required by the ORM */
    }

    private SmartOfferWatch(Guid id, Guid customerId, Guid smartOfferId)
        : base(id)
    {
        CustomerId = customerId;
        SmartOfferId = smartOfferId;

        // TenantId is intentionally NOT a constructor parameter: ABP fills it from ICurrentTenant on insert.
    }

    public static SmartOfferWatch Create(Guid id, Guid customerId, Guid smartOfferId) => new(id, customerId, smartOfferId);
}
