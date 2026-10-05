using System;
using Eksabli.SmartOffers;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace Eksabli.BusinessProfiles;

public class BusinessProfile : AuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }

    public Guid? CategoryId { get; private set; }

    // Customer/admin-facing brand name — deliberately separate from ABP's own Tenant.Name/
    // NormalizedName. Tenant.Name is a technical identifier (unique, used for login/tenant
    // resolution, set once at registration with no self-service rename anywhere); a business may
    // reasonably want customers to see something different (different casing/spacing/punctuation,
    // or a full rebrand) without touching that identifier. Null means "not set yet" — every caller
    // that reads a customer-facing business name falls back to Tenant.Name in that case, so existing
    // businesses keep showing exactly what they show today until they explicitly set this.
    public string? DisplayName { get; private set; }

    public string? LogoBlobName { get; private set; }

    public string? LogoContentType { get; private set; }

    public string? DescriptionAr { get; private set; }

    public string? DescriptionEn { get; private set; }

    public string? Website { get; private set; }

    public string? SocialLinksJson { get; private set; }

    // IANA zone of the business's own clock. "Today", the day boundaries and peak hours on the Business
    // dashboard are computed here, never in the server's zone, so a business never sees yesterday's sales as today's.
    public string TimeZoneId { get; private set; } = BusinessProfileConsts.DefaultTimeZoneId;

    // Manual approval queue until self-serve moderation tooling exists — see
    // docs/eksabli-loyalty-platform/features/08-admin-panel/README.md#business-rules. Every new
    // registration starts Pending; MembershipAppService.JoinAsync blocks joining anything other than
    // Approved, and the Business Portal's own approval guard (Angular) blocks a Pending/Suspended
    // business's own staff from the dashboard.
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

    // Blank/whitespace-only is normalized to null (falls back to Tenant.Name), not stored as an
    // empty string — same reasoning UpdateProfileAsync's other optional text fields already get for
    // free from nullable strings, made explicit here since "" and null must behave identically for
    // the fallback in every reader to be correct.
    public void SetDisplayName(string? displayName) =>
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim();

    public void SetDescription(string? descriptionAr, string? descriptionEn)
    {
        DescriptionAr = descriptionAr;
        DescriptionEn = descriptionEn;
    }

    public void SetWebsite(string? website) => Website = website;

    public void SetSocialLinks(string? socialLinksJson) => SocialLinksJson = socialLinksJson;

    // Validated here, not only in the DTO: a bad zone would silently move every "today" to the wrong day.
    public void SetTimeZone(string timeZoneId)
    {
        Check.NotNullOrWhiteSpace(timeZoneId, nameof(timeZoneId), BusinessProfileConsts.MaxTimeZoneIdLength);
        SmartOfferTiming.ResolveTimeZone(timeZoneId);
        TimeZoneId = timeZoneId;
    }

    public TimeZoneInfo ResolveTimeZone() => SmartOfferTiming.ResolveTimeZone(TimeZoneId);

    public void SetLogo(string? logoBlobName, string? logoContentType)
    {
        LogoBlobName = logoBlobName;
        LogoContentType = logoContentType;
    }
}
