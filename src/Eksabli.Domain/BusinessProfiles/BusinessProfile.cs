using System;
using Volo.Abp;
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

    // Manual approval queue until self-serve moderation tooling exists — see
    // docs/eksabli-loyalty-platform/features/08-admin-panel/README.md#business-rules. Every new
    // registration starts Pending; MembershipAppService.JoinAsync is the one place Suspended is
    // actually enforced today.
    public TenantApprovalStatus ApprovalStatus { get; private set; }

    protected BusinessProfile()
    {
        /* Required by the ORM */
    }

    private BusinessProfile(Guid id, Guid? categoryId)
        : base(id)
    {
        CategoryId = categoryId;
        ApprovalStatus = TenantApprovalStatus.Pending;

        // TenantId is intentionally NOT a constructor parameter — same rule as Membership:
        // ABP populates it automatically from ICurrentTenant.Id at insert time.
    }

    public static BusinessProfile Create(Guid id, Guid? categoryId = null)
    {
        return new BusinessProfile(id, categoryId);
    }

    // Also the reinstatement path — Approve() from Suspended is how a Support/Content Moderator lifts
    // a suspension, so there's no separate Reactivate() method to keep in sync with this one.
    public void Approve()
    {
        if (ApprovalStatus == TenantApprovalStatus.Approved)
        {
            throw new UserFriendlyException("This business is already approved.");
        }

        ApprovalStatus = TenantApprovalStatus.Approved;
    }

    public void Suspend()
    {
        if (ApprovalStatus == TenantApprovalStatus.Suspended)
        {
            throw new UserFriendlyException("This business is already suspended.");
        }

        ApprovalStatus = TenantApprovalStatus.Suspended;
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
