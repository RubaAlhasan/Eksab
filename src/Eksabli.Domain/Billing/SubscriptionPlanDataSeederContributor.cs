using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Eksabli.Features;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace Eksabli.Billing;

// Illustrative tiers from docs/eksabli-loyalty-platform/01-business-strategy.md#revenue-model--pricing
// — flagged there as needing validation against the real target market before publishing, not
// launch-ready numbers. Growth is the trial-default plan (14-30 day full-featured trial).
public class SubscriptionPlanDataSeederContributor : IDataSeedContributor, ITransientDependency
{
    private const string Unlimited = "999999";

    private readonly IRepository<SubscriptionPlan, Guid> _planRepository;

    public SubscriptionPlanDataSeederContributor(IRepository<SubscriptionPlan, Guid> planRepository)
    {
        _planRepository = planRepository;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        if (await _planRepository.GetCountAsync() > 0)
        {
            return;
        }

        await CreatePlanAsync("Starter", 0m, 0m, isTrialDefault: false, new Dictionary<string, string>
        {
            [EksabliFeatures.MaxBranches] = "1",
            [EksabliFeatures.MaxActiveMembers] = "500",
            [EksabliFeatures.MaxCampaigns] = "1",
            [EksabliFeatures.SmsNotifications] = "false",
            [EksabliFeatures.PushNotifications] = "false",
            [EksabliFeatures.Gamification] = "false"
        });

        // USD figures are the same kind of illustrative placeholder as the SYP ones above — not a real
        // conversion (the platform applies none anywhere), just a plausible independent USD price point
        // for a tenant who opts to bill in USD, pending real target-market validation.
        await CreatePlanAsync("Growth", 49m, 5m, isTrialDefault: true, new Dictionary<string, string>
        {
            [EksabliFeatures.MaxBranches] = "5",
            [EksabliFeatures.MaxActiveMembers] = "5000",
            [EksabliFeatures.MaxCampaigns] = Unlimited,
            [EksabliFeatures.SmsNotifications] = "true",
            [EksabliFeatures.PushNotifications] = "true",
            [EksabliFeatures.Gamification] = "true"
        });

        await CreatePlanAsync("Scale", 199m, 20m, isTrialDefault: false, new Dictionary<string, string>
        {
            [EksabliFeatures.MaxBranches] = "25",
            [EksabliFeatures.MaxActiveMembers] = "50000",
            [EksabliFeatures.MaxCampaigns] = Unlimited,
            [EksabliFeatures.SmsNotifications] = "true",
            [EksabliFeatures.PushNotifications] = "true",
            [EksabliFeatures.Gamification] = "true"
        });

        await CreatePlanAsync("Enterprise", 499m, 50m, isTrialDefault: false, new Dictionary<string, string>
        {
            [EksabliFeatures.MaxBranches] = Unlimited,
            [EksabliFeatures.MaxActiveMembers] = Unlimited,
            [EksabliFeatures.MaxCampaigns] = Unlimited,
            [EksabliFeatures.SmsNotifications] = "true",
            [EksabliFeatures.PushNotifications] = "true",
            [EksabliFeatures.Gamification] = "true"
        });
    }

    private async Task CreatePlanAsync(string name, decimal monthlyPriceSyp, decimal monthlyPriceUsd, bool isTrialDefault, Dictionary<string, string> featureLimits)
    {
        var plan = SubscriptionPlan.Create(
            Guid.NewGuid(),
            name,
            monthlyPriceSyp,
            monthlyPriceUsd,
            JsonSerializer.Serialize(featureLimits),
            isTrialDefault);

        await _planRepository.InsertAsync(plan, autoSave: true);
    }
}
