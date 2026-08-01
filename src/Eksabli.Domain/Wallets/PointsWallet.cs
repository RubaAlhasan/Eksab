using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace Eksabli.Wallets;

public class PointsWallet : AuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid MembershipId { get; private set; }

    public Guid? TenantId { get; private set; }

    public int Balance { get; private set; }

    public int LifetimeEarned { get; private set; }

    public int LifetimeRedeemed { get; private set; }

    public Guid? CurrentTierId { get; private set; }

    protected PointsWallet()
    {
        /* Required by the ORM */
    }

    private PointsWallet(Guid id, Guid membershipId)
        : base(id)
    {
        MembershipId = membershipId;
    }

    public static PointsWallet Create(Guid id, Guid membershipId)
    {
        return new PointsWallet(id, membershipId);
    }

    // `points` is the signed delta already meant to be applied to Balance (see PointsTransaction.Points'
    // sign convention). Balance = SUM(Points) over this wallet's transactions is the source of truth;
    // this method just keeps the cached Balance and the Lifetime* reporting counters in lockstep.
    //
    // Adjust and Expire deliberately do NOT move LifetimeEarned/LifetimeRedeemed: LifetimeEarned drives
    // tier-threshold qualification, and a manual correction must never be usable to push a customer into
    // a higher tier. Expire is a balance-only correction, not a reportable redemption.
    public void ApplyTransaction(PointsTransactionType type, int points)
    {
        Balance += points;

        switch (type)
        {
            case PointsTransactionType.Earn:
                LifetimeEarned += points;
                break;
            case PointsTransactionType.Redeem:
                LifetimeRedeemed += -points; // points is negative for Redeem
                break;
            case PointsTransactionType.Refund:
                LifetimeRedeemed -= points; // points is positive for Refund; undoes a prior Redeem
                break;
        }
    }

    public void ChangeTier(Guid? tierId) => CurrentTierId = tierId;
}
