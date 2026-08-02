using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace Eksabli.Billing;

// Platform-wide catalog — not IMultiTenant, per the ERD's "not tenant-scoped" note.
public class SubscriptionPlan : FullAuditedAggregateRoot<Guid>
{
    public string Name { get; private set; }

    public decimal MonthlyPrice { get; private set; }

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

    private SubscriptionPlan(Guid id, string name, decimal monthlyPrice, string featureLimitsJson, bool isTrialDefault)
        : base(id)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), SubscriptionPlanConsts.MaxNameLength);
        MonthlyPrice = monthlyPrice;
        FeatureLimitsJson = featureLimitsJson;
        IsTrialDefault = isTrialDefault;
    }

    public static SubscriptionPlan Create(Guid id, string name, decimal monthlyPrice, string featureLimitsJson, bool isTrialDefault = false)
    {
        return new SubscriptionPlan(id, name, monthlyPrice, featureLimitsJson, isTrialDefault);
    }

    public void SetName(string name) => Name = Check.NotNullOrWhiteSpace(name, nameof(name), SubscriptionPlanConsts.MaxNameLength);

    public void SetMonthlyPrice(decimal monthlyPrice) => MonthlyPrice = monthlyPrice;

    public void SetFeatureLimitsJson(string featureLimitsJson) => FeatureLimitsJson = featureLimitsJson;

    public void SetIsTrialDefault(bool isTrialDefault) => IsTrialDefault = isTrialDefault;
}
