using System;
using Eksabli.Shared;
using Volo.Abp.Domain.Entities;

namespace Eksabli.SmartOffers;

// Child of SmartOffer, with no repository of its own: created, changed and removed only through
// SmartOffer.Update, which is what keeps the stages ordered, non-overlapping and in the offer's currency.
//
// Minutes since local midnight, end exclusive. A stage ending at 24:00 is stored as EndMinute = 1440.
public class SmartOfferPriceStage : Entity<Guid>
{
    public Guid SmartOfferId { get; private set; }

    public int StartMinute { get; private set; }

    public int EndMinute { get; private set; }

    public decimal Price { get; private set; }

    public Currency Currency { get; private set; }

    // Units this stage can sell each local day. Null means unlimited.
    public int? QuantityLimit { get; private set; }

    protected SmartOfferPriceStage()
    {
        /* Required by the ORM */
    }

    internal SmartOfferPriceStage(Guid id, Guid smartOfferId, SmartOfferStageSpec spec)
        : base(id)
    {
        SmartOfferId = smartOfferId;
        Apply(spec);
    }

    internal void Apply(SmartOfferStageSpec spec)
    {
        StartMinute = spec.StartMinute;
        EndMinute = spec.EndMinute;
        Price = spec.Price;
        Currency = spec.Currency;
        QuantityLimit = spec.QuantityLimit;
    }
}
