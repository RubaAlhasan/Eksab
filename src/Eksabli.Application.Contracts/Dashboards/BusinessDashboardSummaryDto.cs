using System;
using System.Collections.Generic;
using Eksabli.Billing;

namespace Eksabli.Dashboards;

// The Business dashboard's KPI payload. Everything money-shaped is a per-currency list and is never summed across
// SYP and USD. Points are the one exception: they are the same unit in both currencies, so they are plain totals.
public class BusinessDashboardSummaryDto
{
    // The business's own zone. Every date on this payload, and the "Today" block, is on this clock.
    public string TimeZoneId { get; set; } = string.Empty;

    public DateOnly Today { get; set; }

    public DateOnly From { get; set; }

    public DateOnly To { get; set; }

    public BusinessTodaySummaryDto TodaySummary { get; set; } = new();

    // Customers who earned points in the window.
    public int ActiveMembers { get; set; }

    // Memberships that joined in the window.
    public int NewMembers { get; set; }

    // Active members who had already earned points before the window started.
    public int ReturningMembers { get; set; }

    public int PointsIssued { get; set; }

    public int PointsRedeemed { get; set; }

    // PointsRedeemed / PointsIssued. Null when nothing was issued, so an empty window doesn't read as 0%.
    public decimal? RedemptionRate { get; set; }

    // Purchase awards (POS checkouts) in the window.
    public int Transactions { get; set; }

    // How many of those purchases recorded a sale amount. Staff can award points without one.
    public int PurchasesWithAmount { get; set; }

    // PurchasesWithAmount / Transactions, as a percentage. Null when there were no purchases.
    public decimal? ValueCoveragePercent { get; set; }

    // Sum of the recorded POS sale amounts, per currency. Covers only the purchases that carried an amount.
    public List<CurrencyAmountDto> RecordedValue { get; set; } = new();

    public int BuyNowSales { get; set; }

    public List<CurrencyAmountDto> BuyNowValue { get; set; } = new();

    // Buy Now orders the customer has reserved and not yet paid for at the counter.
    public int PendingBuyNowOrders { get; set; }

    // Offers that are enabled and have a price in force right now.
    public int LiveOffers { get; set; }
}

public class BusinessTodaySummaryDto
{
    public int Transactions { get; set; }

    public int PointsIssued { get; set; }

    public int PointsRedeemed { get; set; }

    // Distinct customers who earned points today.
    public int CustomersServed { get; set; }

    public int NewCustomers { get; set; }

    public int ReturningCustomers { get; set; }

    public int BuyNowSales { get; set; }

    public List<CurrencyAmountDto> RecordedValue { get; set; } = new();

    public List<CurrencyAmountDto> BuyNowValue { get; set; } = new();
}
