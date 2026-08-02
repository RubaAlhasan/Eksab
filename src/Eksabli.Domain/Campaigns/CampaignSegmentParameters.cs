using System;
using System.Text.Json;

namespace Eksabli.Campaigns;

// Parsed shape of CampaignTargetRule.ParametersJson:
//   Tier         -> TierId
//   Inactive     -> InactiveDays
//   NewCustomer  -> WithinDays
//   All          -> (none)
public class CampaignSegmentParameters
{
    public Guid? TierId { get; set; }

    public int? InactiveDays { get; set; }

    public int? WithinDays { get; set; }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static CampaignSegmentParameters Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new CampaignSegmentParameters();
        }

        try
        {
            return JsonSerializer.Deserialize<CampaignSegmentParameters>(json, JsonOptions) ?? new CampaignSegmentParameters();
        }
        catch (JsonException)
        {
            return new CampaignSegmentParameters();
        }
    }
}
