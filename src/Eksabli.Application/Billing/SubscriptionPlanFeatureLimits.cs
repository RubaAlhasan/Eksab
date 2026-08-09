using System.Collections.Generic;
using System.Text.Json;

namespace Eksabli.Billing;

// SubscriptionPlan.FeatureLimitsJson is documented as a Dictionary<string,string> (see the doc comment
// on SubscriptionPlan.cs) — that's what IFeatureManager.SetForTenantAsync needs, and what
// SubscriptionPlanDataSeederContributor writes. In practice the Admin Panel's plan editor
// (admin-plans.component.ts) has historically round-tripped the *known* keys (MaxBranches,
// MaxActiveMembers, MaxCampaigns, SMSNotifications, PushNotifications) as native JSON numbers/booleans
// rather than strings, which JsonSerializer.Deserialize<Dictionary<string,string>> throws on. Parsing
// via JsonElement here tolerates either shape so a plan saved with non-string values doesn't take down
// trial provisioning / plan changes; the Angular form has separately been fixed to write strings going
// forward, but this keeps already-stored plans working too.
public static class SubscriptionPlanFeatureLimits
{
    public static Dictionary<string, string> Parse(string? featureLimitsJson)
    {
        var result = new Dictionary<string, string>();
        if (string.IsNullOrWhiteSpace(featureLimitsJson))
        {
            return result;
        }

        var raw = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(featureLimitsJson);
        if (raw is null)
        {
            return result;
        }

        foreach (var (key, element) in raw)
        {
            result[key] = element.ValueKind switch
            {
                JsonValueKind.String => element.GetString() ?? string.Empty,
                JsonValueKind.Number => element.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Null => string.Empty,
                _ => element.GetRawText(),
            };
        }

        return result;
    }
}
