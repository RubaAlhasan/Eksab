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

        foreach (var campaign in activeCampaigns)
        {
            var rules = CampaignRules.Parse(campaign.RulesJson);

            if (campaign.Type == CampaignType.DoublePoints)
            {
                // Multiple simultaneous multiplier campaigns take the best single one, not a stack —
                // same "pick the winning value" treatment PosAppService.RecomputeTierAsync gives tiers.
                multiplier = Math.Max(multiplier, rules.Multiplier ?? 2.0m);
            }
            else if (campaign.Type == CampaignType.SpendXGetY &&
                     purchaseAmount.HasValue &&
                     rules.SpendThreshold.HasValue &&
                     purchaseAmount.Value >= rules.SpendThreshold.Value)
            {
                bonusPoints = Math.Max(bonusPoints, rules.BonusPoints ?? 0);
            }
        }

        return new CampaignRulesEvaluationResult { Multiplier = multiplier, BonusPoints = bonusPoints };
    }
}
