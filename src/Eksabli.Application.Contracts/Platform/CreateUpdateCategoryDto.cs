using System;
using System.ComponentModel.DataAnnotations;

namespace Eksabli.Platform;

public class CreateUpdateCategoryDto
{
    [Required]
    [StringLength(CategoryConsts.MaxNameLength)]
    public string NameAr { get; set; } = string.Empty;

    [Required]
    [StringLength(CategoryConsts.MaxNameLength)]
    public string NameEn { get; set; } = string.Empty;

    // The icon is managed only through ICategoryAppService's own Upload/RemoveIconAsync — not settable here,
    // the same way UpdateBusinessProfileDto never carries BusinessProfile's LogoBlobName.
    public Guid? ParentCategoryId { get; set; }
}
