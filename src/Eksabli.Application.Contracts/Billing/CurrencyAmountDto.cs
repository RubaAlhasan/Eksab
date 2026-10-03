using Eksabli.Shared;

namespace Eksabli.Billing;

// One currency's worth of a money aggregate that must never be combined with another currency's —
// see AdminSubscriptionStatsDto.ApproxMrrByCurrency / MrrTrendPointDto.AmountsByCurrency for why: the
// platform applies no SYP<->USD conversion anywhere, so a "total MRR" spanning both currencies would
// have to either convert (not allowed) or silently add two different units together (wrong).
public class CurrencyAmountDto
{
    public Currency Currency { get; set; }

    public decimal Amount { get; set; }
}
