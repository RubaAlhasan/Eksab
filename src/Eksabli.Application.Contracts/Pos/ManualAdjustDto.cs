using System;
using System.ComponentModel.DataAnnotations;
using Eksabli.Wallets;

namespace Eksabli.Pos;

public class ManualAdjustDto
{
    [Required]
    public Guid CustomerId { get; set; }

    [Required]
    public int Points { get; set; }

    [StringLength(PointsTransactionConsts.MaxReasonLength)]
    public string? Reason { get; set; }
}
