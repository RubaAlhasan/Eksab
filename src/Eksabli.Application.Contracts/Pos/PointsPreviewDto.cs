using System;
using Eksabli.Wallets;

namespace Eksabli.Pos;

// The calculation breakdown behind one AwardPointsResultDto — base rule x tier multiplier x campaign
// multiplier, plus a flat campaign bonus (see docs/eksabli-loyalty-platform/07-loyalty-engine.md#8).
// Returned by both PosAppService.PreviewPointsAsync (read-only, called as the cashier types a sale
// amount) and, internally, by the real award path — both go through the exact same private
// PosAppService.ComputePointsAsync, so this can never drift from what an award actually computes.
public class PointsPreviewDto
{
    // The raw base rule's own output — floor(purchaseAmount * pointsPerUnit), or the flat PerVisit
    // amount — with NEITHER the tier NOR any campaign multiplier applied. This is exactly what
    // AwardPointsCoreAsync now records on the Purchase-sourced ledger row; the tier's and any
    // campaign's own contributions are split into their own rows (Source=Tier / Source=Campaign) —
    // see TierExtraPoints/CampaignMultiplierExtraPoints/CampaignBonusPoints below.
    public int BasePoints { get; set; }

    public PointRuleType? RuleType { get; set; }

    public decimal PointsPerUnit { get; set; }

    public decimal TierMultiplier { get; set; } = 1.0m;

    public string? TierName { get; set; }

    // How many more points the customer's current tier is worth than the raw base rate alone —
    // floor(basePoints * TierMultiplier) minus BasePoints. Zero when TierMultiplier is 1 (no tier
    // assigned, or a tier whose own multiplier happens to be exactly 1). AwardPointsCoreAsync attributes
    // this amount to the tier itself (Source=Tier, ReferenceId=the wallet's CurrentTierId) instead of
    // folding it into the plain Purchase row.
    public int TierExtraPoints { get; set; }

    public decimal CampaignMultiplier { get; set; } = 1.0m;

    public string? CampaignName { get; set; }

    // How many more points the campaign multiplier is worth than the tier alone would have earned —
    // floor(basePoints * tierMultiplier * CampaignMultiplier) minus floor(basePoints * tierMultiplier).
    // Zero when CampaignMultiplier is 1 (no active multiplier campaign). Exists purely so
    // AwardPointsCoreAsync can attribute this amount to the multiplier campaign specifically (Source=
    // Campaign, ReferenceId=CampaignId) instead of folding it into the plain Purchase row — see that
    // method's own comment for why (ReportsAppService.GetCampaignPerformanceAsync otherwise has no way
    // to tell a multiplier campaign was ever used).
    public int CampaignMultiplierExtraPoints { get; set; }

    public int CampaignBonusPoints { get; set; }

    public string? BonusCampaignName { get; set; }

    // Carried alongside the two *CampaignName fields above, not just for display — AwardPointsCoreAsync
    // uses these to attribute the real award's multiplier/bonus portions to the right campaigns
    // (Source=Campaign, ReferenceId=<id>), the one thing ReportsAppService.GetCampaignPerformanceAsync
    // actually queries by. See CampaignRulesEvaluationResult's own comment for the full reasoning.
    public Guid? CampaignId { get; set; }

    public Guid? BonusCampaignId { get; set; }

    public int TotalPoints { get; set; }
}
