using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace Eksabli.Campaigns;

public class CampaignDto : FullAuditedEntityDto<Guid>
{
    public Guid? TenantId { get; set; }

    public string NameAr { get; set; } = string.Empty;

    public string NameEn { get; set; } = string.Empty;

    public CampaignType Type { get; set; }

    public string? RulesJson { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public CampaignStatus Status { get; set; }

    public List<CampaignTargetRuleDto> TargetRules { get; set; } = new();
}
