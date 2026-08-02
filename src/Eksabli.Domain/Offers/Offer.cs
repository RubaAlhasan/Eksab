using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace Eksabli.Offers;

// A displayed deal/promotion, distinct from a points-cost Reward — e.g. "20% off this weekend," not
// redeemed with points. See docs/eksabli-loyalty-platform/03-database-design.md#campaigns--notifications.
public class Offer : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }

    public Guid? BranchId { get; private set; }

    public string TitleAr { get; private set; }

    public string TitleEn { get; private set; }

    public string? DescriptionAr { get; private set; }

    public string? DescriptionEn { get; private set; }

    public DateTime StartDate { get; private set; }

    public DateTime EndDate { get; private set; }

    public string? ImageBlobName { get; private set; }

    protected Offer()
    {
        TitleAr = string.Empty;
        TitleEn = string.Empty;
    }

    private Offer(Guid id, string titleAr, string titleEn, DateTime startDate, DateTime endDate)
        : base(id)
    {
        TitleAr = Check.NotNullOrWhiteSpace(titleAr, nameof(titleAr), OfferConsts.MaxTitleLength);
        TitleEn = Check.NotNullOrWhiteSpace(titleEn, nameof(titleEn), OfferConsts.MaxTitleLength);
        SetDateRange(startDate, endDate);
    }

    public static Offer Create(Guid id, string titleAr, string titleEn, DateTime startDate, DateTime endDate)
    {
        return new Offer(id, titleAr, titleEn, startDate, endDate);
    }

    public void SetTitles(string titleAr, string titleEn)
    {
        TitleAr = Check.NotNullOrWhiteSpace(titleAr, nameof(titleAr), OfferConsts.MaxTitleLength);
        TitleEn = Check.NotNullOrWhiteSpace(titleEn, nameof(titleEn), OfferConsts.MaxTitleLength);
    }

    public void SetDescriptions(string? descriptionAr, string? descriptionEn)
    {
        DescriptionAr = descriptionAr;
        DescriptionEn = descriptionEn;
    }

    public void SetBranch(Guid? branchId) => BranchId = branchId;

    public void SetDateRange(DateTime startDate, DateTime endDate)
    {
        if (endDate <= startDate)
        {
            throw new UserFriendlyException("The offer's end date must be after its start date.");
        }

        StartDate = startDate;
        EndDate = endDate;
    }

    public void SetImageBlobName(string? imageBlobName) => ImageBlobName = imageBlobName;
}
