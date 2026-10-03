using System;
using Eksabli.Shared;
using Eksabli.Wallets;

namespace Eksabli.Reports;

public class TransactionExcelDto
{
    public DateTime CreationTime { get; set; }

    public string? CustomerFirstName { get; set; }

    public string? CustomerLastName { get; set; }

    public PointsTransactionType Type { get; set; }

    public int Points { get; set; }

    public PointsTransactionSource Source { get; set; }

    public string? Reason { get; set; }

    // Source=Purchase only — the sale amount/currency that produced this row's points. Null for every
    // other row (Adjust/Redeem/Expire/Referral/Tier/Campaign), same as PointsTransaction.Amount itself.
    public decimal? Amount { get; set; }

    public Currency? Currency { get; set; }
}
