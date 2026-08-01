using System;
using System.ComponentModel.DataAnnotations;

namespace Eksabli.BusinessProfiles;

public class UpdateBusinessProfileDto
{
    public Guid? CategoryId { get; set; }

    [StringLength(BusinessProfileConsts.MaxDescriptionLength)]
    public string? DescriptionAr { get; set; }

    [StringLength(BusinessProfileConsts.MaxDescriptionLength)]
    public string? DescriptionEn { get; set; }

    [StringLength(BusinessProfileConsts.MaxWebsiteLength)]
    public string? Website { get; set; }

    [StringLength(BusinessProfileConsts.MaxSocialLinksJsonLength)]
    public string? SocialLinksJson { get; set; }
}
