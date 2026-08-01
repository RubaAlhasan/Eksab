using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace Eksabli.Rewards;

// Redemption "token" is this row itself, not a separate cache token — unlike a downloaded file, a
// redemption needs a permanent audit trail (RedeemedByEmployeeId/RedeemedBranchId/RedeemedAt), which a
// burned cache entry can't carry. Code doubles as both the QR payload and the typed PIN fallback.
public class Coupon : AuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid RewardId { get; private set; }

    public Guid MembershipId { get; private set; }

    public Guid? TenantId { get; private set; }

    public string Code { get; private set; }

    public CouponStatus Status { get; private set; }

    public DateTime IssuedAt { get; private set; }

    public DateTime? RedeemedAt { get; private set; }

    public Guid? RedeemedByEmployeeId { get; private set; }

    public Guid? RedeemedBranchId { get; private set; }

    protected Coupon()
    {
        Code = string.Empty;
    }

    private Coupon(Guid id, Guid rewardId, Guid membershipId, string code, DateTime issuedAt)
        : base(id)
    {
        RewardId = rewardId;
        MembershipId = membershipId;
        Code = code;
        IssuedAt = issuedAt;
        Status = CouponStatus.Issued;
    }

    public static Coupon Create(Guid id, Guid rewardId, Guid membershipId, string code, DateTime issuedAt)
    {
        return new Coupon(id, rewardId, membershipId, code, issuedAt);
    }

    public void MarkRedeemed(DateTime redeemedAt, Guid redeemedByEmployeeId, Guid? redeemedBranchId)
    {
        if (Status != CouponStatus.Issued)
        {
            throw new UserFriendlyException("This coupon has already been used or is no longer valid.");
        }

        Status = CouponStatus.Redeemed;
        RedeemedAt = redeemedAt;
        RedeemedByEmployeeId = redeemedByEmployeeId;
        RedeemedBranchId = redeemedBranchId;
    }

    public void MarkExpired()
    {
        if (Status == CouponStatus.Issued)
        {
            Status = CouponStatus.Expired;
        }
    }

    public void Cancel()
    {
        if (Status == CouponStatus.Issued)
        {
            Status = CouponStatus.Cancelled;
        }
    }
}
