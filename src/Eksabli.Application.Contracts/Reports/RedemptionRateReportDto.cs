namespace Eksabli.Reports;

public class RedemptionRateReportDto
{
    public int EarnedPoints { get; set; }

    public int RedeemedPoints { get; set; }

    // RedeemedPoints / EarnedPoints for the period, per business (never blended across tenants) —
    // see docs/eksabli-loyalty-platform/features/07-business-dashboard/README.md#business-rules.
    // 0 when nothing was earned in the period, to avoid a divide-by-zero surprising the caller.
    public decimal RedemptionRate { get; set; }
}
