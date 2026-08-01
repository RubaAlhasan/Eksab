using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace Eksabli.Wallets;

// Append-only ledger — rows are never updated or deleted after insert, only created.
public class PointsTransaction : AuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid WalletId { get; private set; }

    public Guid? TenantId { get; private set; }

    public PointsTransactionType Type { get; private set; }

    // Signed delta applied to PointsWallet.Balance — see PointsWallet.ApplyTransaction.
    public int Points { get; private set; }

    public PointsTransactionSource Source { get; private set; }

    // Polymorphic, not FK-constrained: order/reward/campaign id for Earn/Redeem, or the original
    // PointsTransaction.Id being reversed for Refund/Expire.
    public Guid? ReferenceId { get; private set; }

    public DateTime? ExpiresAt { get; private set; }

    // Soft reference (EmployeeAssignment.UserId space) — null for customer/system-triggered rows.
    public Guid? CreatedByEmployeeId { get; private set; }

    // Adjust only — why a staff member manually changed this customer's balance.
    public string? Reason { get; private set; }

    // Snapshot of PointsWallet.CurrentTierId's Multiplier at award time — a later Tier definition
    // change must never retroactively alter historical transaction meaning. Null for transaction
    // types that don't go through the tier-multiplier stage (Redeem/Expire/Adjust/Refund).
    public decimal? TierMultiplierSnapshot { get; private set; }

    protected PointsTransaction()
    {
        /* Required by the ORM */
    }

    private PointsTransaction(
        Guid id,
        Guid walletId,
        PointsTransactionType type,
        int points,
        PointsTransactionSource source,
        Guid? referenceId,
        DateTime? expiresAt,
        Guid? createdByEmployeeId,
        string? reason,
        decimal? tierMultiplierSnapshot)
        : base(id)
    {
        WalletId = walletId;
        Type = type;
        Points = points;
        Source = source;
        ReferenceId = referenceId;
        ExpiresAt = expiresAt;
        CreatedByEmployeeId = createdByEmployeeId;
        Reason = reason;
        TierMultiplierSnapshot = tierMultiplierSnapshot;
    }

    public static PointsTransaction Create(
        Guid id,
        Guid walletId,
        PointsTransactionType type,
        int points,
        PointsTransactionSource source,
        Guid? referenceId = null,
        DateTime? expiresAt = null,
        Guid? createdByEmployeeId = null,
        string? reason = null,
        decimal? tierMultiplierSnapshot = null)
    {
        return new PointsTransaction(id, walletId, type, points, source, referenceId, expiresAt, createdByEmployeeId, reason, tierMultiplierSnapshot);
    }
}
