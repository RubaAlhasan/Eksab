using System;

namespace Eksabli.Reports;

public class TierDistributionDto
{
    // Null = members with no qualifying tier yet.
    public Guid? TierId { get; set; }

    public string? TierName { get; set; }

    public int MemberCount { get; set; }
}
