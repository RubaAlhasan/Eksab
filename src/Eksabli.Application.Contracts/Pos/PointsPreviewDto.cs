using Eksabli.Wallets;

namespace Eksabli.Pos;

// The calculation breakdown behind one AwardPointsResultDto — base rule x tier multiplier x campaign
// multiplier, plus a flat campaign bonus (see docs/eksabli-loyalty-platform/07-loyalty-engine.md#8).
// Returned by both PosAppService.PreviewPointsAsync (read-only, called as the cashier types a sale
// amount) and, internally, by the real award path — both go through the exact same private
// PosAppService.ComputePointsAsync, so this can never drift from what an award actually computes.
public class PointsPreviewDto
{
    public int BasePoints { get; set; }

    public PointRuleType? RuleType { get; set; }

    public decimal PointsPerUnit { get; set; }

    public decimal TierMultiplier { get; set; } = 1.0m;

    public string? TierName { get; set; }

    public decimal CampaignMultiplier { get; set; } = 1.0m;

    public string? CampaignName { get; set; }

    public int CampaignBonusPoints { get; set; }

    public string? BonusCampaignName { get; set; }

    public int TotalPoints { get; set; }
}
