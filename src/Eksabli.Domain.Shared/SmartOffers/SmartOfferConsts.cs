namespace Eksabli.SmartOffers;

public static class SmartOfferConsts
{
    public const int MaxTitleLength = 128;
    public const int MaxDescriptionLength = 1000;
    public const int MaxTimeZoneIdLength = 64;

    // Upper bound on a single price in any currency — a sanity ceiling that catches a mistyped extra zero,
    // not a business rule. Real caps belong to the owner via MinimumPrice / BasePrice.
    public const decimal MaxPrice = 1_000_000_000m;

    // Capacity per slot per day. Shared by DailyQuantity and QuantityLimit.
    public const int MaxQuantity = 1_000_000;

    public const int MaxStagesPerOffer = 12;

    // Units a single customer can hold in one order. Bounds one customer's ability to buy out a slot.
    public const int MaxOrderQuantity = 10;

    // How long a Pending order holds stock before SmartOfferOrderExpirationWorker releases it. The
    // reservation is also capped at the price window's end, so a 15-minute hold quoted at 12:55 for the
    // 11:00–13:00 stage expires at 13:00, not 13:10.
    public const int ReservationWindowMinutes = 15;

    // 8 uppercase hex characters, the same shape a Coupon code uses, so staff read both the same way at the counter.
    public const int CodeLength = 8;

    public const int MaxRejectionReasonLength = 256;

    // The next-price-change search only looks this many local days ahead. Stages recur daily, so a change is
    // always within 24–48h when stages exist; a larger horizon would only add work for no real case.
    public const int NextChangeLookaheadDays = 2;

    public const int MinutesPerDay = 24 * 60;
}
