using System;

namespace Eksabli.Campaigns;

// Customer-safe view of a live campaign or offer. Deliberately excludes
// RulesJson and TargetRules — a customer must not see how a business segments
// its members, only that a promotion applies to them.
public class CustomerCampaignDto
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    // Resolved from the tenant so the app can render the business without a
    // second lookup per row.
    public string BusinessName { get; set; } = string.Empty;

    public string NameAr { get; set; } = string.Empty;

    public string NameEn { get; set; } = string.Empty;

    public CampaignType Type { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }
}
