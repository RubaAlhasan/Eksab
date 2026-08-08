using System;
using System.ComponentModel.DataAnnotations;
using Eksabli.Branches;
using Eksabli.BusinessProfiles;

namespace Eksabli.Businesses;

public class RegisterBusinessDto
{
    // 128 mirrors ABP's own Tenant.Name column length.
    [Required]
    [StringLength(128)]
    public string BusinessName { get; set; } = string.Empty;

    public Guid? CategoryId { get; set; }

    [StringLength(BusinessProfileConsts.MaxDescriptionLength)]
    public string? DescriptionAr { get; set; }

    [StringLength(BusinessProfileConsts.MaxDescriptionLength)]
    public string? DescriptionEn { get; set; }

    [StringLength(BusinessProfileConsts.MaxWebsiteLength)]
    public string? Website { get; set; }

    // Stored as BusinessProfile.SocialLinksJson (a freeform blob — same field the tenant's own
    // profile-edit endpoint, UpdateBusinessProfileDto, already exposes as raw JSON). These two
    // discrete fields are just a nicer admin-facing shape for the platform's two most common
    // networks; BusinessAppService.RegisterAsync serializes them into that same JSON column rather
    // than introducing a new column/schema.
    [StringLength(BusinessProfileConsts.MaxWebsiteLength)]
    public string? InstagramUrl { get; set; }

    [StringLength(BusinessProfileConsts.MaxWebsiteLength)]
    public string? FacebookUrl { get; set; }

    [Required]
    [StringLength(BranchConsts.MaxNameLength)]
    public string BranchName { get; set; } = string.Empty;

    [StringLength(BranchConsts.MaxAddressLength)]
    public string? BranchAddress { get; set; }

    [StringLength(BranchConsts.MaxPhoneLength)]
    public string? BranchPhone { get; set; }

    // Real fields already on Branch (Branch.SetLocation) — needed to eventually compute
    // distance-to-customer for a "nearest branch" feature. No such distance-calculation feature
    // exists yet (checked — nothing in the backend reads these today), so this only captures the
    // coordinates at signup time for that future use, it doesn't compute or expose distance itself.
    public double? BranchLatitude { get; set; }

    public double? BranchLongitude { get; set; }

    [Required]
    [EmailAddress]
    public string OwnerEmail { get; set; } = string.Empty;

    [Required]
    [StringLength(128, MinimumLength = 6)]
    public string OwnerPassword { get; set; } = string.Empty;
}
