namespace Eksabli.SmartOffers;

// Persisted as int — append new members, never renumber. Each member maps to one
// ISmartPricingStrategy in Eksabli.SmartOffers.Pricing; Flash/Quantity/LastMinute strategies slot in here.
public enum SmartPricingStrategy
{
    // One price for the whole local service day (BasePrice), capped by DailyQuantity.
    Fixed = 0,

    // Price follows the offer's daily time stages (SmartOfferPriceStage), each with its own price and quantity.
    TimeBased = 1
}
