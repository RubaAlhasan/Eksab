using System;
using Volo.Abp.Domain.Entities;

namespace Eksabli.Campaigns;

// Child entity — no TenantId/IMultiTenant of its own (see
// docs/eksabli-loyalty-platform/03-database-design.md#campaigns--notifications), and no repository:
// only ever created/queried through its owning Campaign.
public class CampaignTargetRule : Entity<Guid>
{
    public Guid CampaignId { get; private set; }

    public CampaignTargetRuleSegmentType SegmentType { get; private set; }

    // Segment-specific criteria — see Campaigns.CampaignSegmentParameters for the shape
    // (TierId/InactiveDays/WithinDays) CampaignSegmentEvaluator parses it with.
    public string? ParametersJson { get; private set; }

    protected CampaignTargetRule()
    {
        /* Required by the ORM */
    }

    private CampaignTargetRule(Guid id, Guid campaignId, CampaignTargetRuleSegmentType segmentType, string? parametersJson)
        : base(id)
    {
        CampaignId = campaignId;
        SegmentType = segmentType;
        ParametersJson = parametersJson;
    }

    // internal — Campaign.AddTargetRule is the only sanctioned entry point (same assembly).
    internal static CampaignTargetRule Create(Guid id, Guid campaignId, CampaignTargetRuleSegmentType segmentType, string? parametersJson)
    {
        return new CampaignTargetRule(id, campaignId, segmentType, parametersJson);
    }
}
