using System.Collections.Generic;

namespace Eksabli.Billing;

// One month's worth of real, collected revenue for the Admin Dashboard's "Platform MRR" trend chart
// (prototype/admin/dashboard.html's 7-month bar chart) — deliberately built from *paid* Invoice rows
// (AdminSubscriptionAppService.GetMrrTrendAsync groups Invoice.Amount by Invoice.PaidAt's month and by
// Invoice.Currency, Host-scoped via Disable<IMultiTenant>()), not fabricated. This is a proxy for "MRR"
// (actual collected revenue per month) rather than a true point-in-time recurring-revenue snapshot —
// there's no subscription-status history table to compute the latter from — but it's real, DB-backed
// data, same spirit as AdminSubscriptionStatsDto.ApproxMrrByCurrency being an approximation of the
// current month. AmountsByCurrency only lists currencies with at least one paid invoice that month —
// no forced zero entries — and is never summed across currencies (no conversion is applied anywhere).
public class MrrTrendPointDto
{
    public int Year { get; set; }

    public int Month { get; set; }

    public List<CurrencyAmountDto> AmountsByCurrency { get; set; } = new();
}
