using System;
using Volo.Abp.Application.Dtos;

namespace Eksabli.Wallets;

public class PointsTransactionDto : EntityDto<Guid>
{
    public Guid WalletId { get; set; }

    public PointsTransactionType Type { get; set; }

    public int Points { get; set; }

    public PointsTransactionSource Source { get; set; }

    public Guid? ReferenceId { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public Guid? CreatedByEmployeeId { get; set; }

    public string? Reason { get; set; }

    public decimal? TierMultiplierSnapshot { get; set; }

    public DateTime CreationTime { get; set; }
}
