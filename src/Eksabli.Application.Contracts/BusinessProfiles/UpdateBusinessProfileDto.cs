using System;
using System.ComponentModel.DataAnnotations;

namespace Eksabli.BusinessProfiles;

public class UpdateBusinessProfileDto
{
    public Guid? CategoryId { get; set; }

    [StringLength(BusinessProfileConsts.MaxDisplayNameLength)]
    public string? DisplayName { get; set; }

    [StringLength(BusinessProfileConsts.MaxDescriptionLength)]
    public string? DescriptionAr { get; set; }

    [StringLength(BusinessProfileConsts.MaxDescriptionLength)]
    public string? DescriptionEn { get; set; }

    [StringLength(BusinessProfileConsts.MaxWebsiteLength)]
    public string? Website { get; set; }

    [StringLength(BusinessProfileConsts.MaxSocialLinksJsonLength)]
    public string? SocialLinksJson { get; set; }

    [StringLength(BusinessProfileConsts.MaxTimeZoneIdLength)]
    public string? TimeZoneId { get; set; }

    // Null switches points expiry off: the whole profile is replaced on every save, same as the other fields here.
    [Range(BusinessProfileConsts.MinPointsExpiryMonths, BusinessProfileConsts.MaxPointsExpiryMonths)]
    public int? PointsExpiryMonths { get; set; }
}
