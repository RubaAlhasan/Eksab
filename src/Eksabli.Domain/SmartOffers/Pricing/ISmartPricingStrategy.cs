using System;
using System.Collections.Generic;

namespace Eksabli.SmartOffers.Pricing;

// The extension point for pricing rules. SmartOffer owns the state and the invariants; a strategy owns how that
// state maps to a price at an instant. Adding Flash Deals, Quantity-Based or Last-Minute pricing means adding a
// strategy here and a SmartPricingStrategy member, without touching SmartOffer's validation or persistence.
public interface ISmartPricingStrategy
{
    // The price in force at nowUtc, or null when nothing is on sale (outside every window, or outside validity).
    SmartPriceQuote? Quote(SmartOffer offer, DateTime nowUtc);

    // Every instant after afterUtc (ascending) where the quote might change. SmartOffer compares quotes at each
    // one, so a strategy only needs to list candidate boundaries, not decide which of them actually matter.
    IEnumerable<DateTime> ChangePointsUtc(SmartOffer offer, DateTime afterUtc);
}

public static class SmartPricingStrategies
{
    private static readonly ISmartPricingStrategy FixedStrategy = new FixedPricingStrategy();
    private static readonly ISmartPricingStrategy TimeBasedStrategy = new TimeBasedPricingStrategy();

    public static ISmartPricingStrategy For(SmartPricingStrategy strategy) => strategy switch
    {
        SmartPricingStrategy.Fixed => FixedStrategy,
        SmartPricingStrategy.TimeBased => TimeBasedStrategy,
        _ => throw new ArgumentOutOfRangeException(nameof(strategy), strategy, "Unknown pricing strategy."),
    };
}
