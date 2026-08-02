using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Eksabli.Campaigns;

public class CreateUpdateCampaignDto
{
    [Required]
    [StringLength(CampaignConsts.MaxNameLength)]
    public string NameAr { get; set; } = string.Empty;

    [Required]
    [StringLength(CampaignConsts.MaxNameLength)]
    public string NameEn { get; set; } = string.Empty;

    [Required]
    public CampaignType Type { get; set; }

    [StringLength(CampaignConsts.MaxRulesJsonLength)]
    public string? RulesJson { get; set; }

    [Required]
    public DateTime StartDate { get; set; }

    [Required]
    public DateTime EndDate { get; set; }

    public List<CreateUpdateCampaignTargetRuleDto> TargetRules { get; set; } = new();
}
