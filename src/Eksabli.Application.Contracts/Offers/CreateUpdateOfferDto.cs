using System;
using System.ComponentModel.DataAnnotations;

namespace Eksabli.Offers;

public class CreateUpdateOfferDto
{
    public Guid? BranchId { get; set; }

    [Required]
    [StringLength(OfferConsts.MaxTitleLength)]
    public string TitleAr { get; set; } = string.Empty;

    [Required]
    [StringLength(OfferConsts.MaxTitleLength)]
    public string TitleEn { get; set; } = string.Empty;

    [StringLength(OfferConsts.MaxDescriptionLength)]
    public string? DescriptionAr { get; set; }

    [StringLength(OfferConsts.MaxDescriptionLength)]
    public string? DescriptionEn { get; set; }

    [Required]
    public DateTime StartDate { get; set; }

    [Required]
    public DateTime EndDate { get; set; }

    [StringLength(OfferConsts.MaxImageBlobNameLength)]
    public string? ImageBlobName { get; set; }
}
