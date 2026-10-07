using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace Eksabli.Reviews;

// One customer's rating (+ optional comment) of one business. At most one per
// customer per business — see EksabliDbContext's unique index on (TenantId, CustomerId) — so a
// second CreateOrUpdateMyReviewAsync call edits this row in place rather than adding another, the same
// "upsert, not append" shape the prototype's star rating implies. Editable (unlike Follow/SmartOfferWatch,
// which are delete-and-recreate), so AuditedAggregateRoot tracks the last edit time too.
public class Review : AuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid CustomerId { get; private set; }

    public Guid? TenantId { get; private set; }

    public int Rating { get; private set; }

    public string? Comment { get; private set; }

    protected Review()
    {
        /* Required by the ORM */
    }

    private Review(Guid id, Guid customerId, int rating, string? comment)
        : base(id)
    {
        CustomerId = customerId;
        SetRating(rating, comment);
    }

    public static Review Create(Guid id, Guid customerId, int rating, string? comment)
    {
        return new Review(id, customerId, rating, comment);
    }

    public void SetRating(int rating, string? comment)
    {
        Check.Range(rating, nameof(rating), ReviewConsts.MinRating, ReviewConsts.MaxRating);

        Rating = rating;
        Comment = comment;
    }
}
