using System;
using System.Collections.Generic;
using Eksabli.Billing;
using Eksabli.SmartOffers;

namespace Eksabli.Dashboards;

// One Smart Offer's Buy Now results for the window. Every offer appears, including those with no orders, so a
// dead deal is visible instead of silently missing.
public class BusinessOfferPerformanceDto
{
    public Guid OfferId { get; set; }

    public string TitleAr { get; set; } = string.Empty;

    public string TitleEn { get; set; } = string.Empty;

    // Where the offer is in its lifecycle right now, not within the window.
    public SmartOfferStatus Status { get; set; }

    public int Placed { get; set; }

    public int Pending { get; set; }

    public int Completed { get; set; }

    // Rejected, cancelled or expired. Together with Completed, these are the orders that reached an outcome.
    public int Lapsed { get; set; }

    // Completed / (Completed + Lapsed). Pending orders are excluded because they haven't been decided yet. Null when
    // nothing has been decided.
    public decimal? CompletionRate { get; set; }

    // Sales value for completed orders, per currency. Normally one entry, unless the offer's currency was changed later.
    public List<CurrencyAmountDto> CompletedValue { get; set; } = new();

    public DateTime? LastOrderAt { get; set; }
}
