namespace Eksabli.Rewards;

public static class CouponConsts
{
    // 8 uppercase hex characters, e.g. "3B02543F". Doubles as the QR payload and the typed fallback,
    // so it has to survive being read aloud across a counter — hex keeps it unambiguous (no O/0, I/1
    // confusion beyond the digits themselves) and short enough to type without a scanner.
    public const int CodeLength = 8;

    // How long a Pending coupon holds its reservation before RedemptionReservationWorker releases it.
    //
    // This is a *counter-queue* timeout, not a security window: the customer taps redeem while standing
    // at the till, and staff approve within seconds. Fifteen minutes is generous enough to absorb a
    // queue, a card-machine detour or a staff member finding a manager for a high-value reward, while
    // still returning the points the same visit if the sale falls through.
    public const int PendingWindowMinutes = 15;

    // What an INBOUND code field accepts, as opposed to what is stored. Staff type the code the way
    // the customer app displays it — grouped, sometimes hyphenated — and the POS endpoints strip that
    // before matching. Validating against CodeLength would reject "3B02 543F" with an opaque
    // "method arguments are not valid" before any of that normalization ran.
    public const int MaxSubmittedCodeLength = CodeLength + 4;

    public const int MaxRejectionReasonLength = 256;
}
