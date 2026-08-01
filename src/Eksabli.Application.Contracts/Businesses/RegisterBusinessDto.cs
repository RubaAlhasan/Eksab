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

    [Required]
    [StringLength(BranchConsts.MaxNameLength)]
    public string BranchName { get; set; } = string.Empty;

    [StringLength(BranchConsts.MaxAddressLength)]
    public string? BranchAddress { get; set; }

    [StringLength(BranchConsts.MaxPhoneLength)]
    public string? BranchPhone { get; set; }

    [Required]
    [EmailAddress]
    public string OwnerEmail { get; set; } = string.Empty;

    [Required]
    [StringLength(128, MinimumLength = 6)]
    public string OwnerPassword { get; set; } = string.Empty;
}
