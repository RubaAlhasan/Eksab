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

    [StringLength(CategoryConsts.MaxIconBlobNameLength)]
    public string? IconBlobName { get; set; }

    public Guid? ParentCategoryId { get; set; }
}
