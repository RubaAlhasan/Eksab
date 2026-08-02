using System;
using Volo.Abp.Domain.Entities.Auditing;

namespace Eksabli.Engagement;

// Which customer earned which badge, and when — an audit trail, not a catalog entity, same treatment
// as Rewards.Coupon. No TenantId of its own: tenant scope is derived from MembershipId's own Membership.
public class AchievementAward : AuditedAggregateRoot<Guid>
{
    public Guid MembershipId { get; private set; }

    public Guid AchievementId { get; private set; }

    public DateTime AwardedAt { get; private set; }

    protected AchievementAward()
    {
        /* Required by the ORM */
    }

    private AchievementAward(Guid id, Guid membershipId, Guid achievementId, DateTime awardedAt)
        : base(id)
    {
        MembershipId = membershipId;
        AchievementId = achievementId;
        AwardedAt = awardedAt;
    }

    public static AchievementAward Create(Guid id, Guid membershipId, Guid achievementId, DateTime awardedAt)
    {
        return new AchievementAward(id, membershipId, achievementId, awardedAt);
    }
}
