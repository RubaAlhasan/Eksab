using System;
using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Timing;

namespace Eksabli.Campaigns;

// Real-time evaluation mode from
// docs/eksabli-loyalty-platform/features/05-campaigns-notifications/README.md#business-rules —
// DoublePoints/SpendXGetY campaigns, evaluated inline by PosAppService.ComputePointsAsync inside the
// point-award request itself. This is the seam that comment used to describe as "doesn't exist yet."
// The *other* evaluation mode (scheduled segment sweep) lives in CampaignSweepWorker.
public class CampaignRulesEngine : ICampaignRulesEngine, ITransientDependency
{
    private readonly IRepository<Campaign, Guid> _campaignRepository;
    private readonly IClock _clock;

    public CampaignRulesEngine(IRepository<Campaign, Guid> campaignRepository, IClock clock)
    {
        _campaignRepository = campaignRepository;
        _clock = clock;
    }

    public async Task<CampaignRulesEvaluationResult> EvaluateAsync(decimal? purchaseAmount)
    {
        var now = _clock.Now;

        var activeCampaigns = await _campaignRepository.GetListAsync(c =>
            c.Status == CampaignStatus.Active &&
            c.StartDate <= now && c.EndDate >= now &&
            (c.Type == CampaignType.DoublePoints || c.Type == CampaignType.SpendXGetY));

        var multiplier = 1.0m;
        var bonusPoints = 0;
        string? multiplierCampaignName = null;
        string? bonusCampaignName = null;
        Guid? multiplierCampaignId = null;
        Guid? bonusCampaignId = null;

        foreach (var campaign in activeCampaigns)
        {
            var rules = CampaignRules.Parse(campaign.RulesJson);

            if (campaign.Type == CampaignType.DoublePoints)
            {
                // Multiple simultaneous multiplier campaigns take the best single one, not a stack —
                // same "pick the winning value" treatment PosAppService.RecomputeTierAsync gives tiers.
                var candidateMultiplier = rules.Multiplier ?? 2.0m;
                if (candidateMultiplier > multiplier)
                {
                    multiplier = candidateMultiplier;
                    multiplierCampaignName = campaign.NameEn;
                    multiplierCampaignId = campaign.Id;
                }
            }
            else if (campaign.Type == CampaignType.SpendXGetY &&
                     purchaseAmount.HasValue &&
                     rules.SpendThreshold.HasValue &&
                     purchaseAmount.Value >= rules.SpendThreshold.Value)
            {
                var candidateBonus = rules.BonusPoints ?? 0;
                if (candidateBonus > bonusPoints)
                {
                    bonusPoints = candidateBonus;
                    bonusCampaignName = campaign.NameEn;
                    bonusCampaignId = campaign.Id;
                }
            }
        }

        return new CampaignRulesEvaluationResult
        {
            Multiplier = multiplier,
            BonusPoints = bonusPoints,
            MultiplierCampaignName = multiplierCampaignName,
            BonusCampaignName = bonusCampaignName,
            MultiplierCampaignId = multiplierCampaignId,
            BonusCampaignId = bonusCampaignId
        };
    }
}
