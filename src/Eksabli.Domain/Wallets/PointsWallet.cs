using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace Eksabli.Wallets;

public class PointsWallet : AuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid MembershipId { get; private set; }

    public Guid? TenantId { get; private set; }

    public int Balance { get; private set; }

    // Points committed to Pending redemptions that no staff member has approved yet.
    //
    // Deliberately NOT part of Balance, and deliberately NOT a ledger row: `Balance = SUM(Points)`
    // over this wallet's transactions is the invariant the whole points system rests on (see
    // ApplyTransaction), and a reservation is not a movement of points — it is a claim on points that
    // may never move. Writing one to the ledger would either break that sum or require a
    // compensating row for every abandoned redemption, turning the customer's own transaction
    // history into a log of things that did not happen.
    //
    // Spendable = AvailableBalance. Balance stays the number the customer is shown, because from
    // their side the points are still theirs until staff hand over the reward.
    public int Reserved { get; private set; }

    public int AvailableBalance => Balance - Reserved;

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

    // ---------------------------------------------------------------------------------------------
    // Reservations. Three transitions, and every Reserve must be followed by exactly one Commit or
    // Release — RedemptionReservationWorker is the backstop that guarantees the second half happens
    // even if the customer walks away and no staff member ever touches the coupon.
    // ---------------------------------------------------------------------------------------------

    // Holds `points` against a Pending redemption. Checks AvailableBalance, not Balance, so two
    // concurrent redemptions can't each reserve the same points.
    public void Reserve(int points)
    {
        if (points <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(points), "A reservation must be positive.");
        }

        if (AvailableBalance < points)
        {
            throw new UserFriendlyException("You don't have enough points to redeem this reward.");
        }

        Reserved += points;
    }

    // Staff approved: the hold becomes a real debit. The caller writes the matching Redeem ledger row
    // and passes the same signed delta to ApplyTransaction — this only drops the hold.
    public void CommitReservation(int points)
    {
        GuardReleasable(points);
        Reserved -= points;
    }

    // Staff rejected, or the window closed: the hold evaporates. No ledger row, because no points ever
    // moved — the customer's history should not record a redemption that never happened.
    public void ReleaseReservation(int points)
    {
        GuardReleasable(points);
        Reserved -= points;
    }

    private void GuardReleasable(int points)
    {
        if (points <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(points), "A reservation must be positive.");
        }

        // Defensive: a double-release would silently hand the customer free spending power. The state
        // machine on Coupon already prevents it, so reaching here means a caller bypassed it.
        if (Reserved < points)
        {
            throw new AbpException(
                $"Wallet {Id} holds {Reserved} reserved points; cannot release {points}.");
        }
    }

    public void ChangeTier(Guid? tierId) => CurrentTierId = tierId;
}
