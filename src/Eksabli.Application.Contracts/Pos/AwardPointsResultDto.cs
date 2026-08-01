using System;

namespace Eksabli.Pos;

public class AwardPointsResultDto
{
    public Guid TransactionId { get; set; }

    public int PointsAwarded { get; set; }

    public int NewBalance { get; set; }

    public Guid? NewTierId { get; set; }

    public string? NewTierName { get; set; }
}
