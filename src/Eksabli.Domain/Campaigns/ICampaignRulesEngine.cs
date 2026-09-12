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
}
