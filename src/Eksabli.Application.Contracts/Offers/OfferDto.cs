using System;
using Volo.Abp.Application.Dtos;

namespace Eksabli.Offers;

public class OfferDto : FullAuditedEntityDto<Guid>
{
    public Guid? TenantId { get; set; }

    public Guid? BranchId { get; set; }

    public string TitleAr { get; set; } = string.Empty;

    public string TitleEn { get; set; } = string.Empty;

    public string? DescriptionAr { get; set; }

    public string? DescriptionEn { get; set; }

    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public string? ImageBlobName { get; set; }
}
