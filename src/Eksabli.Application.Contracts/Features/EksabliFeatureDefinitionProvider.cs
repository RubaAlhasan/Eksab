using Eksabli.Localization;
using Volo.Abp.Features;
using Volo.Abp.Localization;
using Volo.Abp.Validation.StringValues;

namespace Eksabli.Features;

// Plan entitlements — SubscriptionPlan.FeatureLimitsJson values get pushed into these per-tenant on
// trial start / plan change (see BusinessAppService.RegisterAsync, BillingAppService.ChangePlanAsync).
// Enforcement anywhere else in the codebase is a FeatureChecker.GetAsync<int>(...) call, not new
// business logic — see BranchAppService.CreateAsync for the one concrete example wired up so far.
public class EksabliFeatureDefinitionProvider : FeatureDefinitionProvider
{
    public override void Define(IFeatureDefinitionContext context)
    {
        var myGroup = context.AddGroup(EksabliFeatures.GroupName);

        myGroup.AddFeature(
            EksabliFeatures.MaxBranches,
            defaultValue: "1",
            displayName: L("Feature:MaxBranches"),
            valueType: new FreeTextStringValueType(new NumericValueValidator(1, 100000)));

        myGroup.AddFeature(
            EksabliFeatures.MaxActiveMembers,
            defaultValue: "500",
            displayName: L("Feature:MaxActiveMembers"),
            valueType: new FreeTextStringValueType(new NumericValueValidator(1, int.MaxValue)));

        myGroup.AddFeature(
            EksabliFeatures.MaxCampaigns,
            defaultValue: "1",
            displayName: L("Feature:MaxCampaigns"),
            valueType: new FreeTextStringValueType(new NumericValueValidator(0, int.MaxValue)));

        myGroup.AddFeature(
            EksabliFeatures.SmsNotifications,
            defaultValue: "false",
            displayName: L("Feature:SMSNotifications"),
            valueType: new ToggleStringValueType());

        myGroup.AddFeature(
            EksabliFeatures.PushNotifications,
            defaultValue: "false",
            displayName: L("Feature:PushNotifications"),
            valueType: new ToggleStringValueType());
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<EksabliResource>(name);
    }
}
