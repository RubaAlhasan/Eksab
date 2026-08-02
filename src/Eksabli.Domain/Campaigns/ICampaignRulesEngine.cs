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
}
