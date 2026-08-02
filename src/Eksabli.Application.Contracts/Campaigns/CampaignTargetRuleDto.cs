using System;
using Volo.Abp.Application.Dtos;

namespace Eksabli.Campaigns;

public class CampaignTargetRuleDto : EntityDto<Guid>
{
    public Guid CampaignId { get; set; }

    public CampaignTargetRuleSegmentType SegmentType { get; set; }

    public string? ParametersJson { get; set; }
}
