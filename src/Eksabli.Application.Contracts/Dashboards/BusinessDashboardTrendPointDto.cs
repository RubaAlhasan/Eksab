using System;
using System.Collections.Generic;
using Eksabli.Billing;

namespace Eksabli.Dashboards;

// One business-local day of the trend. Zero-filled: a quiet day still appears, so the chart has no gaps.
public class BusinessDashboardTrendPointDto
{
    public DateOnly Date { get; set; }

    public int PointsIssued { get; set; }

    public int PointsRedeemed { get; set; }

    public int Transactions { get; set; }

    public List<CurrencyAmountDto> RecordedValue { get; set; } = new();

    public int BuyNowSales { get; set; }

    public List<CurrencyAmountDto> BuyNowValue { get; set; } = new();
}
