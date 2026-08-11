using System;
using Volo.Abp.Application.Dtos;

namespace Eksabli.BusinessProfiles;

public class BusinessProfileDto : AuditedEntityDto<Guid>
{
    public Guid? TenantId { get; set; }

    public Guid? CategoryId { get; set; }

    public string? LogoBlobName { get; set; }

    public string? DescriptionAr { get; set; }

    public string? DescriptionEn { get; set; }

    public string? Website { get; set; }

    public string? SocialLinksJson { get; set; }

    public TenantApprovalStatus ApprovalStatus { get; set; }
}
