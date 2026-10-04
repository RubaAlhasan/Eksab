namespace Eksabli.SmartOffers;

// Owner-facing lifecycle, derived from the aggregate's state at a point in time (SmartOffer.GetStatus) and never
// stored. A deal whose last stage ends at 18:00 moves to BetweenStages on its own, without anyone touching the row.
public enum SmartOfferStatus
{
    // Owner switched the offer off. Customers cannot see or order it.
    Paused = 0,

    // Enabled, but today's validity starts later (ValidFrom is in the future).
    Scheduled = 1,

    // Enabled and a price applies right now.
    Live = 2,

    // Enabled and inside its validity dates, but no stage covers the current local time.
    BetweenStages = 3,

    // Enabled, but ValidTo is before today (local date of the offer's own time zone).
    Expired = 4
}
