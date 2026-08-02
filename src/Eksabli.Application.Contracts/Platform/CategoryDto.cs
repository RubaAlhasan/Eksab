using System;
using Volo.Abp.Application.Dtos;

namespace Eksabli.Platform;

public class CategoryDto : FullAuditedEntityDto<Guid>
{
    public string NameAr { get; set; } = string.Empty;

    public string NameEn { get; set; } = string.Empty;

    public string? IconBlobName { get; set; }

    public Guid? ParentCategoryId { get; set; }
}
