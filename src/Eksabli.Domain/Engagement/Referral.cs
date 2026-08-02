using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace Eksabli.Engagement;

// Tracks a customer inviting another customer into a specific business. An audit trail, not a catalog
// entity — no soft delete, same treatment as Rewards.Coupon.
public class Referral : AuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid ReferrerMembershipId { get; private set; }

    public Guid RefereeCustomerId { get; private set; }

    public Guid? TenantId { get; private set; }

    public ReferralStatus Status { get; private set; }

    protected Referral()
    {
        /* Required by the ORM */
    }

    private Referral(Guid id, Guid referrerMembershipId, Guid refereeCustomerId)
        : base(id)
    {
        ReferrerMembershipId = referrerMembershipId;
        RefereeCustomerId = refereeCustomerId;
        Status = ReferralStatus.Pending;
    }

    public static Referral Create(Guid id, Guid referrerMembershipId, Guid refereeCustomerId)
    {
        return new Referral(id, referrerMembershipId, refereeCustomerId);
    }

    public void Complete()
    {
        if (Status != ReferralStatus.Pending)
        {
            throw new UserFriendlyException("This referral has already been completed.");
        }

        Status = ReferralStatus.Completed;
    }

    public void MarkRewarded()
    {
        if (Status != ReferralStatus.Completed)
        {
            throw new UserFriendlyException("This referral must be completed before it can be rewarded.");
        }

        Status = ReferralStatus.Rewarded;
    }
}
