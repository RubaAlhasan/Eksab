using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace Eksabli.Platform;

// Platform-wide business category taxonomy for discovery — not tenant-scoped (no IMultiTenant): every
// tenant's BusinessProfile.CategoryId points into this same shared list. Self-referencing for
// subcategories; ParentCategoryId is a soft reference (no FK), same convention as other cross-aggregate
// links in this codebase.
public class Category : FullAuditedAggregateRoot<Guid>
{
    public string NameAr { get; private set; }

    public string NameEn { get; private set; }

    public string? IconBlobName { get; private set; }

    public Guid? ParentCategoryId { get; private set; }

    protected Category()
    {
        NameAr = string.Empty;
        NameEn = string.Empty;
    }

    private Category(Guid id, string nameAr, string nameEn, Guid? parentCategoryId)
        : base(id)
    {
        NameAr = Check.NotNullOrWhiteSpace(nameAr, nameof(nameAr), CategoryConsts.MaxNameLength);
        NameEn = Check.NotNullOrWhiteSpace(nameEn, nameof(nameEn), CategoryConsts.MaxNameLength);
        ParentCategoryId = parentCategoryId;
    }

    public static Category Create(Guid id, string nameAr, string nameEn, Guid? parentCategoryId = null)
    {
        return new Category(id, nameAr, nameEn, parentCategoryId);
    }

    public void SetNames(string nameAr, string nameEn)
    {
        NameAr = Check.NotNullOrWhiteSpace(nameAr, nameof(nameAr), CategoryConsts.MaxNameLength);
        NameEn = Check.NotNullOrWhiteSpace(nameEn, nameof(nameEn), CategoryConsts.MaxNameLength);
    }

    public void SetIconBlobName(string? iconBlobName) => IconBlobName = iconBlobName;

    public void SetParent(Guid? parentCategoryId)
    {
        if (parentCategoryId == Id)
        {
            throw new UserFriendlyException("A category can't be its own parent.");
        }

        ParentCategoryId = parentCategoryId;
    }
}
