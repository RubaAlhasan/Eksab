using System.Text.Json;

namespace Eksabli.Campaigns;

// Parsed shape of Campaign.RulesJson — one shape covers every campaign type's effect rather than a
// class hierarchy per type, since only a couple of fields apply to any given type:
//   DoublePoints  -> Multiplier
//   SpendXGetY    -> SpendThreshold, BonusPoints
//   Birthday      -> DaysBefore, BonusPoints
//   WinBack/Vip/NewCustomer -> BonusPoints
public class CampaignRules
{
    public decimal? Multiplier { get; set; }

    public decimal? SpendThreshold { get; set; }

    public int? BonusPoints { get; set; }

    public int? DaysBefore { get; set; }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // Admin-authored free text — a malformed value degrades to "no rules" rather than breaking the
    // sweep/POS pipeline for every other campaign.
    public static CampaignRules Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new CampaignRules();
        }

        try
        {
            return JsonSerializer.Deserialize<CampaignRules>(json, JsonOptions) ?? new CampaignRules();
        }
        catch (JsonException)
        {
            return new CampaignRules();
        }
    }
}
