using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Eksabli.Shared;

namespace Eksabli.Billing;

// Platform-wide catalog — not IMultiTenant, per the ERD's "not tenant-scoped" note.
public class SubscriptionPlan : FullAuditedAggregateRoot<Guid>
{
    public string Name { get; private set; }

    // Two independent, admin-set prices rather than one price + a stored exchange rate — the platform
    // applies no SYP<->USD conversion anywhere (deliberate product decision), and a plan must be
    // subscribable by a tenant billed in either currency, so both numbers have to exist side by side.
    public decimal MonthlyPriceSyp { get; private set; }

    public decimal MonthlyPriceUsd { get; private set; }

    // Dictionary<string,string> serialized — well-known keys: Eksabli.MaxBranches,
    // Eksabli.MaxActiveMembers, Eksabli.MaxCampaigns, Eksabli.SMSNotifications, Eksabli.PushNotifications.
    // Pushed into ABP Feature Management per tenant on trial start / plan change, never read directly.
    public string FeatureLimitsJson { get; private set; }

    // Exactly one seeded plan should have this true — the plan new tenants trial on.
    public bool IsTrialDefault { get; private set; }

    protected SubscriptionPlan()
    {
        Name = string.Empty;
        FeatureLimitsJson = "{}";
    }

    private SubscriptionPlan(Guid id, string name, decimal monthlyPriceSyp, decimal monthlyPriceUsd, string featureLimitsJson, bool isTrialDefault)
        : base(id)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), SubscriptionPlanConsts.MaxNameLength);
        MonthlyPriceSyp = monthlyPriceSyp;
        MonthlyPriceUsd = monthlyPriceUsd;
        FeatureLimitsJson = featureLimitsJson;
        IsTrialDefault = isTrialDefault;
    }

    public static SubscriptionPlan Create(Guid id, string name, decimal monthlyPriceSyp, decimal monthlyPriceUsd, string featureLimitsJson, bool isTrialDefault = false)
    {
        return new SubscriptionPlan(id, name, monthlyPriceSyp, monthlyPriceUsd, featureLimitsJson, isTrialDefault);
    }

    public void SetName(string name) => Name = Check.NotNullOrWhiteSpace(name, nameof(name), SubscriptionPlanConsts.MaxNameLength);

    public void SetMonthlyPriceSyp(decimal monthlyPriceSyp) => MonthlyPriceSyp = monthlyPriceSyp;

    public void SetMonthlyPriceUsd(decimal monthlyPriceUsd) => MonthlyPriceUsd = monthlyPriceUsd;

    public void SetFeatureLimitsJson(string featureLimitsJson) => FeatureLimitsJson = featureLimitsJson;

    public void SetIsTrialDefault(bool isTrialDefault) => IsTrialDefault = isTrialDefault;

    public decimal GetMonthlyPrice(Currency currency) => currency == Currency.Syp ? MonthlyPriceSyp : MonthlyPriceUsd;
}
