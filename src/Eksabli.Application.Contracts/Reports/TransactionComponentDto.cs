using System;
using Eksabli.Wallets;

namespace Eksabli.Reports;

// One raw PointsTransaction ledger row inside a TransactionListItemDto's Components list — the "where
// did these points come from" breakdown behind a single grouped row. See
// ReportsAppService.GetTransactionsListAsync for how ReferenceName is resolved (polymorphic per Source).
public class TransactionComponentDto
{
    public Guid Id { get; set; }

    public PointsTransactionSource Source { get; set; }

    public int Points { get; set; }

    // Human-readable name of whatever ReferenceId points to: the Tier name, Campaign name, the redeemed
    // Reward's name, or the referred customer's name. Null for Source=Purchase/Manual, which have
    // nothing further to resolve.
    public string? ReferenceName { get; set; }

    // Snapshot of the Tier's Multiplier at award time (Source=Tier components only).
    public decimal? TierMultiplier { get; set; }

    // Adjust only — why a staff member manually changed this customer's balance.
    public string? Reason { get; set; }
}
