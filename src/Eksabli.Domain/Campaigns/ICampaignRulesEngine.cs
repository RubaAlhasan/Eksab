using System;
using System.Threading.Tasks;

namespace Eksabli.Campaigns;

public interface ICampaignRulesEngine
{
    Task<CampaignRulesEvaluationResult> EvaluateAsync(decimal? purchaseAmount);
}

public class CampaignRulesEvaluationResult
{
    public decimal Multiplier { get; set; } = 1.0m;

    public int BonusPoints { get; set; }

    // Name of whichever active campaign actually produced Multiplier/BonusPoints above — null when
    // no campaign contributed (Multiplier stayed at its 1.0 default / BonusPoints stayed 0). Exists
    // so callers that display a calculation breakdown (PosAppService.PreviewPointsAsync) can label the
    // row with the real campaign name instead of a generic "a campaign is active" string.
    public string? MultiplierCampaignName { get; set; }

    public string? BonusCampaignName { get; set; }

    // Ids of the same two campaigns, alongside their names above — needed so a caller that commits a
    // REAL award (PosAppService.AwardPointsCoreAsync) can attribute a PointsTransaction to the actual
    // campaign (Source=Campaign, ReferenceId=<id>), the same way CampaignSweepWorker already tags its
    // own batch-evaluated campaigns (Birthday/WinBack/Vip/NewCustomer). Without this, a real-time
    // campaign's contribution to a POS award had nowhere to attribute to, and
    // ReportsAppService.GetCampaignPerformanceAsync's "Rewarded Members"/"Bonus Points Awarded" stats
    // for DoublePoints/SpendXGetY campaigns never moved no matter how many real sales applied them.
    public Guid? MultiplierCampaignId { get; set; }

    public Guid? BonusCampaignId { get; set; }
}
