using System.Threading.Tasks;

namespace Eksabli.Wallets;

// Shared by PosAppService (after a purchase-earn) and Engagement.ReferralCompletionService (after a
// referral bonus) — any code path that changes PointsWallet.LifetimeEarned must recompute the tier
// through here, not duplicate the qualifying-tier lookup, or CurrentTierId goes stale until the
// wallet's next purchase. Campaigns.CampaignSegmentEvaluator's Tier-segment targeting reads
// CurrentTierId directly, so a stale value isn't just a display issue.
public interface ITierRecomputeService
{
    Task RecomputeAsync(PointsWallet wallet);
}
