using System.Collections.Generic;

namespace Eksabli.Billing;

// Platform-wide subscription stats for the Admin Portal's Subscriptions page stat row — computed
// server-side (DB-level GroupBy/Count, not a client-side load of every subscription row) so the
// Angular page needs one call instead of three (previously: two separate GetListAsync calls for
// active/trialing counts, fired concurrently alongside the paginated list call — see
// AdminSubscriptionAppService.GetStatsAsync for the aggregation). ApproxMrrByCurrency is a TRUE total
// now (every active subscription, grouped by plan then by currency), not the old client-side version's
// first-500-rows cap — one entry per currency that has at least one active subscription, never summed
// across currencies (no conversion is applied anywhere).
public class AdminSubscriptionStatsDto
{
    public int ActiveCount { get; set; }

    public int TrialingCount { get; set; }

    public List<CurrencyAmountDto> ApproxMrrByCurrency { get; set; } = new();
}
