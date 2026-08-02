using System;
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
}
