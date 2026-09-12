using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace Eksabli.Memberships;

public class Membership : AuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid CustomerId { get; private set; }

    public Guid? TenantId { get; private set; }

    public DateTime JoinedAt { get; private set; }

    public MembershipStatus Status { get; private set; }

    // Null until this member's first GetMyReferralCodeAsync call — most memberships never refer
    // anyone, so this is generated lazily (Engagement.ReferralAppService), not at Create() time, and
    // this class doesn't generate it itself (a domain entity shouldn't need repository access just to
    // guarantee uniqueness). Unique per (TenantId, ReferralCode), not globally — see EksabliDbContext's
    // index config. Unlike Rewards.Coupon.Code (deliberately global, per CouponAppService
    // .GenerateUniqueCodeAsync's own comment), a referral code never has to identify the business on
    // its own: a referee always joins a specific, already-known tenant
    // (Memberships.JoinBusinessDto.TenantId), so the code is only ever looked up already scoped to the
    // right business.
    public string? ReferralCode { get; private set; }

    protected Membership()
    {
        /* Required by the ORM */
    }

    private Membership(Guid id, Guid customerId, DateTime joinedAt)
        : base(id)
    {
        CustomerId = customerId;
        JoinedAt = joinedAt;
        Status = MembershipStatus.Active;

        // TenantId is intentionally NOT a constructor parameter — ABP populates it
        // automatically from ICurrentTenant.Id at insert time because this class
        // implements IMultiTenant. Setting it here would fight the framework.
    }

    public static Membership Create(Guid id, Guid customerId, DateTime joinedAt)
    {
        return new Membership(id, customerId, joinedAt);
    }

    public void Freeze()
    {
        Status = MembershipStatus.Frozen;
    }

    public void Reactivate()
    {
        Status = MembershipStatus.Active;
    }

    // Set once, by ReferralAppService.GetMyReferralCodeAsync, after it has already confirmed
    // uniqueness within this tenant — this method itself doesn't re-check, same "dumb domain method,
    // validating caller" shape as ChangePlan/ApprovePendingPlanChange on Billing.TenantSubscription.
    public void SetReferralCode(string code)
    {
        ReferralCode = code;
    }
}
