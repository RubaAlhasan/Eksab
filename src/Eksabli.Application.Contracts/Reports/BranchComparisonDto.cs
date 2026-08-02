using System;

namespace Eksabli.Reports;

// Redemption-side branch attribution only — the earn side (PosAppService.AwardPointsCoreAsync) doesn't
// record which branch a purchase happened at yet, so per-branch points-issued isn't computable from
// today's data model. That's a Feature 02 pipeline gap, not something this report papers over.
public class BranchComparisonDto
{
    public Guid BranchId { get; set; }

    public string BranchName { get; set; } = string.Empty;

    public int RedemptionCount { get; set; }
}
