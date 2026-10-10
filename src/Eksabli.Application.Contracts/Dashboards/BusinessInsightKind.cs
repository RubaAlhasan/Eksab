namespace Eksabli.Dashboards;

// Append new members, never renumber: the UI and any stored references depend on these values.
public enum BusinessInsightKind
{
    // A live offer has taken no orders in InactiveOfferWindowDays.
    OfferWithoutOrders = 0,

    // Rewards are at or below LowStockThreshold. Count is how many.
    LowStockRewards = 1,

    // Pending Buy Now orders expire within BuyNowExpiryWarningMinutes. Count is how many.
    BuyNowExpiringSoon = 2,

    // One of the busiest local hours has no live deal covering it. Hour is which one.
    UncoveredPeakHour = 3,

    // Too few purchases carry a sale amount for the recorded-value totals to be trusted. Percent is the coverage.
    LowValueCoverage = 4,
}
