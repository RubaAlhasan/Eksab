using System.ComponentModel.DataAnnotations;

namespace Eksabli.Campaigns;

public class CreateUpdateCampaignTargetRuleDto
{
    [Required]
    public CampaignTargetRuleSegmentType SegmentType { get; set; }

    [StringLength(CampaignTargetRuleConsts.MaxParametersJsonLength)]
    public string? ParametersJson { get; set; }
}
