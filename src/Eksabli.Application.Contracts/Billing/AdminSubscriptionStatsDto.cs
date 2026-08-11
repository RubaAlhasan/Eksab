namespace Eksabli.Billing;

// Platform-wide subscription stats for the Admin Portal's Subscriptions page stat row — computed
// server-side (DB-level GroupBy/Count, not a client-side load of every subscription row) so the
// Angular page needs one call instead of three (previously: two separate GetListAsync calls for
// active/trialing counts, fired concurrently alongside the paginated list call — see
// AdminSubscriptionAppService.GetStatsAsync for the aggregation). ApproxMrr is a TRUE total now
// (every active subscription, grouped by plan), not the old client-side version's first-500-rows cap.
public class AdminSubscriptionStatsDto
{
    public int ActiveCount { get; set; }

    public int TrialingCount { get; set; }

    public decimal ApproxMrr { get; set; }
}
