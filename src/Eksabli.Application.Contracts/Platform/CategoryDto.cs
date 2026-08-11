using System;
using Volo.Abp.Application.Dtos;

namespace Eksabli.Platform;

public class CategoryDto : FullAuditedEntityDto<Guid>
{
    public string NameAr { get; set; } = string.Empty;

    public string NameEn { get; set; } = string.Empty;

    public string? IconBlobName { get; set; }

    public Guid? ParentCategoryId { get; set; }

    // Cross-aggregate count of BusinessProfiles (any tenant) currently set to this category — not a
    // Category property, computed by CategoryAppService via IRepository<BusinessProfile> with the
    // IMultiTenant filter disabled (same pattern AdminTenantAppService uses for platform-wide reads).
    public int BusinessCount { get; set; }
}
