using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace Eksabli.BusinessProfiles;

public class BusinessProfile : AuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }

    public Guid? CategoryId { get; private set; }

    public string? LogoBlobName { get; private set; }

    public string? DescriptionAr { get; private set; }

    public string? DescriptionEn { get; private set; }

    public string? Website { get; private set; }

    public string? SocialLinksJson { get; private set; }

    protected BusinessProfile()
    {
        /* Required by the ORM */
    }

    private BusinessProfile(Guid id, Guid? categoryId)
        : base(id)
    {
        CategoryId = categoryId;

        // TenantId is intentionally NOT a constructor parameter — same rule as Membership:
        // ABP populates it automatically from ICurrentTenant.Id at insert time.
    }

    public static BusinessProfile Create(Guid id, Guid? categoryId = null)
    {
        return new BusinessProfile(id, categoryId);
    }

    public void SetCategory(Guid? categoryId) => CategoryId = categoryId;

    public void SetDescription(string? descriptionAr, string? descriptionEn)
    {
        DescriptionAr = descriptionAr;
        DescriptionEn = descriptionEn;
    }

    public void SetWebsite(string? website) => Website = website;

    public void SetSocialLinks(string? socialLinksJson) => SocialLinksJson = socialLinksJson;

    public void SetLogo(string? logoBlobName) => LogoBlobName = logoBlobName;
}
