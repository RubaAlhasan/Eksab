using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Eksabli.Shared;
using Eksabli.SmartOffers;
using Eksabli.Wallets;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;
using Volo.Abp.MultiTenancy;

namespace Eksabli.Dashboards;

// The one place the Dashboard 360 pages read points and sales over time. Both dashboards call it. The only difference
// between them is the tenant scope: the Business dashboard runs inside its own tenant, which ABP filters automatically,
// and the Admin dashboard passes platformWide to disable that filter and see every business.
public class DashboardMetricsService : ITransientDependency
{
    private readonly IRepository<PointsTransaction, Guid> _transactionRepository;
    private readonly IRepository<SmartOfferOrder, Guid> _orderRepository;
    private readonly IDataFilter _dataFilter;
    private readonly IAsyncQueryableExecuter _asyncExecuter;

    public DashboardMetricsService(
        IRepository<PointsTransaction, Guid> transactionRepository,
        IRepository<SmartOfferOrder, Guid> orderRepository,
        IDataFilter dataFilter,
        IAsyncQueryableExecuter asyncExecuter)
    {
        _transactionRepository = transactionRepository;
        _orderRepository = orderRepository;
        _dataFilter = dataFilter;
        _asyncExecuter = asyncExecuter;
    }

    // Buckets everything in [startUtc, endUtc) by UTC hour. Only hours with activity are returned; callers zero-fill
    // whatever days or hours they need. Grouping happens in the database, so the row count is bounded by the number of
    // hours in the window, not by the number of transactions.
    public async Task<List<HourlyFlow>> GetHourlyFlowAsync(DateTime startUtc, DateTime endUtc, bool platformWide)
    {
        using var tenantFilter = platformWide ? _dataFilter.Disable<IMultiTenant>() : null;

        var transactions = await _transactionRepository.GetQueryableAsync();
        var orders = await _orderRepository.GetQueryableAsync();

        var pointsByHour = await _asyncExecuter.ToListAsync(
            transactions
                .Where(t => t.CreationTime >= startUtc && t.CreationTime < endUtc)
                .GroupBy(t => new { t.CreationTime.Year, t.CreationTime.Month, t.CreationTime.Day, t.CreationTime.Hour })
                .Select(g => new
                {
                    g.Key.Year,
                    g.Key.Month,
                    g.Key.Day,
                    g.Key.Hour,
                    Issued = g.Sum(t => t.Type == PointsTransactionType.Earn ? t.Points : 0),
                    Redeemed = g.Sum(t => t.Type == PointsTransactionType.Redeem ? -t.Points : 0),
                    Transactions = g.Count(t => t.Type == PointsTransactionType.Earn && t.Source == PointsTransactionSource.Purchase),
                    WithAmount = g.Count(t =>
                        t.Type == PointsTransactionType.Earn && t.Source == PointsTransactionSource.Purchase && t.Amount != null),
                }));

        // Purchase awards carry the sale amount on their base Purchase row only (see PointsTransaction.Amount).
        var purchaseValueByHour = await _asyncExecuter.ToListAsync(
            transactions
                .Where(t => t.CreationTime >= startUtc && t.CreationTime < endUtc
                    && t.Type == PointsTransactionType.Earn && t.Source == PointsTransactionSource.Purchase
                    && t.Amount != null && t.Currency != null)
                .GroupBy(t => new
                {
                    t.CreationTime.Year,
                    t.CreationTime.Month,
                    t.CreationTime.Day,
                    t.CreationTime.Hour,
                    Currency = t.Currency!.Value,
                })
                .Select(g => new
                {
                    g.Key.Year,
                    g.Key.Month,
                    g.Key.Day,
                    g.Key.Hour,
                    g.Key.Currency,
                    Amount = g.Sum(t => t.Amount!.Value),
                }));

        var buyNowByHour = await _asyncExecuter.ToListAsync(
            orders
                .Where(o => o.Status == SmartOfferOrderStatus.Completed
                    && o.CompletedAt != null && o.CompletedAt >= startUtc && o.CompletedAt < endUtc)
                .GroupBy(o => new
                {
                    Year = o.CompletedAt!.Value.Year,
                    Month = o.CompletedAt!.Value.Month,
                    Day = o.CompletedAt!.Value.Day,
                    Hour = o.CompletedAt!.Value.Hour,
                    o.Currency,
                })
                .Select(g => new
                {
                    g.Key.Year,
                    g.Key.Month,
                    g.Key.Day,
                    g.Key.Hour,
                    g.Key.Currency,
                    Count = g.Count(),
                    Amount = g.Sum(o => o.TotalAmount),
                }));

        var hours = new Dictionary<DateTime, HourlyFlow>();
        HourlyFlow HourAt(int year, int month, int day, int hour)
        {
            var key = new DateTime(year, month, day, hour, 0, 0, DateTimeKind.Utc);
            if (!hours.TryGetValue(key, out var flow))
            {
                flow = new HourlyFlow { HourUtc = key };
                hours[key] = flow;
            }

            return flow;
        }

        foreach (var row in pointsByHour)
        {
            var flow = HourAt(row.Year, row.Month, row.Day, row.Hour);
            flow.PointsIssued += row.Issued;
            flow.PointsRedeemed += row.Redeemed;
            flow.Transactions += row.Transactions;
            flow.PurchasesWithAmount += row.WithAmount;
        }

        foreach (var row in purchaseValueByHour)
        {
            var flow = HourAt(row.Year, row.Month, row.Day, row.Hour);
            flow.RecordedValue[row.Currency] = flow.RecordedValue.GetValueOrDefault(row.Currency) + row.Amount;
        }

        foreach (var row in buyNowByHour)
        {
            var flow = HourAt(row.Year, row.Month, row.Day, row.Hour);
            flow.BuyNowSales += row.Count;
            flow.BuyNowValue[row.Currency] = flow.BuyNowValue.GetValueOrDefault(row.Currency) + row.Amount;
        }

        return hours.Values.OrderBy(h => h.HourUtc).ToList();
    }
}
