using System;
using System.Collections.Generic;
using System.Linq;
using Eksabli.Billing;
using Eksabli.Shared;

namespace Eksabli.Dashboards;

// Additive totals for one slice of time: an hour, a day, or a whole window. Points are summed as-is, since they are
// the same unit in both currencies. Money stays keyed by currency and is never converted or combined across currencies.
public class FlowTotals
{
    public int PointsIssued { get; set; }

    public int PointsRedeemed { get; set; }

    // Purchase awards (POS checkouts).
    public int Transactions { get; set; }

    // Of those, how many recorded a sale amount.
    public int PurchasesWithAmount { get; set; }

    // Completed Buy Now sales.
    public int BuyNowSales { get; set; }

    public Dictionary<Currency, decimal> RecordedValue { get; } = new();

    public Dictionary<Currency, decimal> BuyNowValue { get; } = new();

    public static FlowTotals Sum(IEnumerable<FlowTotals> slices)
    {
        var total = new FlowTotals();
        foreach (var slice in slices)
        {
            total.PointsIssued += slice.PointsIssued;
            total.PointsRedeemed += slice.PointsRedeemed;
            total.Transactions += slice.Transactions;
            total.PurchasesWithAmount += slice.PurchasesWithAmount;
            total.BuyNowSales += slice.BuyNowSales;
            AddAmounts(total.RecordedValue, slice.RecordedValue);
            AddAmounts(total.BuyNowValue, slice.BuyNowValue);
        }

        return total;
    }

    public List<CurrencyAmountDto> RecordedValueAmounts() => ToAmounts(RecordedValue);

    public List<CurrencyAmountDto> BuyNowValueAmounts() => ToAmounts(BuyNowValue);

    public static void AddAmounts(Dictionary<Currency, decimal> target, Dictionary<Currency, decimal> source)
    {
        foreach (var (currency, amount) in source)
        {
            target[currency] = target.GetValueOrDefault(currency) + amount;
        }
    }

    public static List<CurrencyAmountDto> ToAmounts(IDictionary<Currency, decimal> totals) =>
        totals
            .OrderBy(pair => pair.Key)
            .Select(pair => new CurrencyAmountDto { Currency = pair.Key, Amount = pair.Value })
            .ToList();
}

// The totals for one UTC hour. Hours are the unit the queries bucket by, so each caller can re-bucket them into its
// own zone (a day, or a local hour of the week) without a query that depends on the server's clock.
public class HourlyFlow : FlowTotals
{
    public DateTime HourUtc { get; set; }
}
