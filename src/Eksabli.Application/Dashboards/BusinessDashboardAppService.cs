using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Eksabli.BusinessProfiles;
using Eksabli.Memberships;
using Eksabli.Permissions;
using Eksabli.Rewards;
using Eksabli.SmartOffers;
using Eksabli.Wallets;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Services;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;

namespace Eksabli.Dashboards;

// Business Portal Dashboard 360. Everything here runs inside the caller's own tenant. The tenant is taken from the
// ambient tenant (ICurrentTenant), never from the request, so one business can't ask for another's numbers.
// Dates are read on the business's own clock (BusinessProfile.TimeZoneId), so "today" is the business's today.
[RemoteService(IsEnabled = false)]
[Authorize(EksabliPermissions.Reports.Default)]
public class BusinessDashboardAppService : ApplicationService, IBusinessDashboardAppService
{
    private readonly DashboardMetricsService _metrics;
    private readonly IRepository<PointsTransaction, Guid> _transactionRepository;
    private readonly IRepository<Membership, Guid> _membershipRepository;
    private readonly IRepository<SmartOfferOrder, Guid> _orderRepository;
    private readonly ISmartOfferRepository _offerRepository;
    private readonly IRepository<Reward, Guid> _rewardRepository;
    private readonly IRepository<BusinessProfile, Guid> _profileRepository;

    public BusinessDashboardAppService(
        DashboardMetricsService metrics,
        IRepository<PointsTransaction, Guid> transactionRepository,
        IRepository<Membership, Guid> membershipRepository,
        IRepository<SmartOfferOrder, Guid> orderRepository,
        ISmartOfferRepository offerRepository,
        IRepository<Reward, Guid> rewardRepository,
        IRepository<BusinessProfile, Guid> profileRepository)
    {
        _metrics = metrics;
        _transactionRepository = transactionRepository;
        _membershipRepository = membershipRepository;
        _orderRepository = orderRepository;
        _offerRepository = offerRepository;
        _rewardRepository = rewardRepository;
        _profileRepository = profileRepository;
    }

    public async Task<BusinessDashboardSummaryDto> GetSummaryAsync(DashboardRangeDto input)
    {
        var zone = await ResolveZoneAsync();
        var now = Clock.Now;
        var today = DashboardTimeWindow.TodayIn(now, zone.Zone);
        var (from, to) = DashboardTimeWindow.Resolve(input, today);

        var (startUtc, endUtc) = DashboardTimeWindow.ToUtcBounds(from, to, zone.Zone);
        var (todayStartUtc, todayEndUtc) = DashboardTimeWindow.ToUtcBounds(today, today, zone.Zone);

        var range = FlowTotals.Sum(await _metrics.GetHourlyFlowAsync(startUtc, endUtc, platformWide: false));
        var todayFlow = FlowTotals.Sum(await _metrics.GetHourlyFlowAsync(todayStartUtc, todayEndUtc, platformWide: false));

        var orders = await _orderRepository.GetQueryableAsync();

        return new BusinessDashboardSummaryDto
        {
            TimeZoneId = zone.Id,
            Today = today,
            From = from,
            To = to,
            TodaySummary = new BusinessTodaySummaryDto
            {
                Transactions = todayFlow.Transactions,
                PointsIssued = todayFlow.PointsIssued,
                PointsRedeemed = todayFlow.PointsRedeemed,
                CustomersServed = await CountEarningMembersAsync(todayStartUtc, todayEndUtc),
                NewCustomers = await CountNewMembersAsync(todayStartUtc, todayEndUtc),
                ReturningCustomers = await CountReturningMembersAsync(todayStartUtc, todayEndUtc),
                BuyNowSales = todayFlow.BuyNowSales,
                RecordedValue = todayFlow.RecordedValueAmounts(),
                BuyNowValue = todayFlow.BuyNowValueAmounts(),
            },
            ActiveMembers = await CountEarningMembersAsync(startUtc, endUtc),
            NewMembers = await CountNewMembersAsync(startUtc, endUtc),
            ReturningMembers = await CountReturningMembersAsync(startUtc, endUtc),
            PointsIssued = range.PointsIssued,
            PointsRedeemed = range.PointsRedeemed,
            RedemptionRate = Ratio(range.PointsRedeemed, range.PointsIssued),
            Transactions = range.Transactions,
            PurchasesWithAmount = range.PurchasesWithAmount,
            ValueCoveragePercent = Percent(range.PurchasesWithAmount, range.Transactions),
            RecordedValue = range.RecordedValueAmounts(),
            BuyNowSales = range.BuyNowSales,
            BuyNowValue = range.BuyNowValueAmounts(),
            PendingBuyNowOrders = await AsyncExecuter.CountAsync(
                orders.Where(o => o.Status == SmartOfferOrderStatus.Pending && o.ReservationExpiresAt > now)),
            LiveOffers = await CountLiveOffersAsync(now),
        };
    }

    public async Task<List<BusinessDashboardTrendPointDto>> GetTrendsAsync(DashboardRangeDto input)
    {
        var zone = await ResolveZoneAsync();
        var (from, to) = DashboardTimeWindow.Resolve(input, DashboardTimeWindow.TodayIn(Clock.Now, zone.Zone));
        var (startUtc, endUtc) = DashboardTimeWindow.ToUtcBounds(from, to, zone.Zone);

        var flows = await _metrics.GetHourlyFlowAsync(startUtc, endUtc, platformWide: false);
        var totalsByDay = flows
            .GroupBy(f => DashboardTimeWindow.LocalDateOf(f.HourUtc, zone.Zone))
            .ToDictionary(g => g.Key, g => FlowTotals.Sum(g));

        return DashboardTimeWindow.EachDay(from, to)
            .Select(day =>
            {
                var totals = totalsByDay.GetValueOrDefault(day) ?? new FlowTotals();
                return new BusinessDashboardTrendPointDto
                {
                    Date = day,
                    PointsIssued = totals.PointsIssued,
                    PointsRedeemed = totals.PointsRedeemed,
                    Transactions = totals.Transactions,
                    RecordedValue = totals.RecordedValueAmounts(),
                    BuyNowSales = totals.BuyNowSales,
                    BuyNowValue = totals.BuyNowValueAmounts(),
                };
            })
            .ToList();
    }

    public async Task<List<PeakHourCellDto>> GetPeakHoursAsync(DashboardRangeDto input)
    {
        var zone = await ResolveZoneAsync();
        var (from, to) = DashboardTimeWindow.Resolve(input, DashboardTimeWindow.TodayIn(Clock.Now, zone.Zone));
        var (startUtc, endUtc) = DashboardTimeWindow.ToUtcBounds(from, to, zone.Zone);

        var activity = new Dictionary<(int Day, int Hour), int>();
        foreach (var flow in await _metrics.GetHourlyFlowAsync(startUtc, endUtc, platformWide: false))
        {
            // An hourly bucket is attributed to the local hour its start falls in. Zones offset by a whole number of
            // hours are exact. Half-hour zones blur by up to 30 minutes, which is fine for a peak-hours heatmap.
            var (localDate, localMinute) = SmartOfferTiming.ToLocal(flow.HourUtc, zone.Zone);
            var key = ((int)localDate.DayOfWeek, localMinute / 60);
            activity[key] = activity.GetValueOrDefault(key) + flow.Transactions + flow.BuyNowSales;
        }

        return Enumerable.Range(0, 7)
            .SelectMany(day => Enumerable.Range(0, 24).Select(hour => new PeakHourCellDto
            {
                DayOfWeek = day,
                Hour = hour,
                Activity = activity.GetValueOrDefault((day, hour)),
            }))
            .ToList();
    }

    public async Task<List<BusinessOfferPerformanceDto>> GetOfferPerformanceAsync(DashboardRangeDto input)
    {
        var zone = await ResolveZoneAsync();
        var now = Clock.Now;
        var (from, to) = DashboardTimeWindow.Resolve(input, DashboardTimeWindow.TodayIn(now, zone.Zone));
        var (startUtc, endUtc) = DashboardTimeWindow.ToUtcBounds(from, to, zone.Zone);

        // Every offer is listed, including the ones with no orders in the window, so a dead deal is visible.
        var (offers, _) = await _offerRepository.GetListAsync();

        var orders = await _orderRepository.GetQueryableAsync();
        // Grouped by currency as well: an offer's currency can be changed later, so older orders may be in another one.
        var rows = await AsyncExecuter.ToListAsync(
            orders
                .Where(o => o.PlacedAt >= startUtc && o.PlacedAt < endUtc)
                .GroupBy(o => new { o.SmartOfferId, o.Status, o.Currency })
                .Select(g => new
                {
                    g.Key.SmartOfferId,
                    g.Key.Status,
                    g.Key.Currency,
                    Count = g.Count(),
                    Amount = g.Sum(o => o.TotalAmount),
                    LastPlacedAt = g.Max(o => o.PlacedAt),
                }));

        return offers
            .Select(offer =>
            {
                var mine = rows.Where(r => r.SmartOfferId == offer.Id).ToList();
                var completed = mine.Where(r => r.Status == SmartOfferOrderStatus.Completed).ToList();
                var completedCount = completed.Sum(r => r.Count);
                var lapsedCount = mine.Where(r => IsLapsed(r.Status)).Sum(r => r.Count);

                return new BusinessOfferPerformanceDto
                {
                    OfferId = offer.Id,
                    TitleAr = offer.TitleAr,
                    TitleEn = offer.TitleEn,
                    Status = offer.GetStatus(now),
                    Placed = mine.Sum(r => r.Count),
                    Pending = mine.Where(r => r.Status == SmartOfferOrderStatus.Pending).Sum(r => r.Count),
                    Completed = completedCount,
                    Lapsed = lapsedCount,
                    CompletionRate = Ratio(completedCount, completedCount + lapsedCount),
                    CompletedValue = FlowTotals.ToAmounts(
                        completed
                            .GroupBy(r => r.Currency)
                            .ToDictionary(g => g.Key, g => g.Sum(r => r.Amount))),
                    LastOrderAt = mine.Count == 0 ? null : mine.Max(r => r.LastPlacedAt),
                };
            })
            .OrderByDescending(o => o.Placed)
            .ThenBy(o => o.TitleEn)
            .ToList();
    }

    public async Task<List<BusinessInsightDto>> GetInsightsAsync()
    {
        var zone = await ResolveZoneAsync();
        var now = Clock.Now;
        var insights = new List<BusinessInsightDto>();

        // Only offers that are enabled and inside their validity dates can be "not selling". A paused or expired deal
        // isn't a problem to fix.
        var (enabledOffers, _) = await _offerRepository.GetListAsync(enabledOnly: true);
        var onSale = enabledOffers
            .Where(o => o.GetStatus(now) is SmartOfferStatus.Live or SmartOfferStatus.BetweenStages)
            .ToList();

        var orders = await _orderRepository.GetQueryableAsync();

        var recentStart = now.AddDays(-DashboardDefinitions.InactiveOfferWindowDays);
        var offersWithRecentOrders = (await AsyncExecuter.ToListAsync(
            orders.Where(o => o.PlacedAt >= recentStart).Select(o => o.SmartOfferId).Distinct())).ToHashSet();

        foreach (var offer in onSale.Where(o => !offersWithRecentOrders.Contains(o.Id)))
        {
            insights.Add(new BusinessInsightDto
            {
                Kind = BusinessInsightKind.OfferWithoutOrders,
                Severity = DashboardInsightSeverity.Info,
                RelatedId = offer.Id,
                NameAr = offer.TitleAr,
                NameEn = offer.TitleEn,
            });
        }

        var rewards = await _rewardRepository.GetQueryableAsync();
        var lowStockCount = await AsyncExecuter.CountAsync(
            rewards.Where(r => r.StockRemaining != null && r.StockRemaining <= DashboardDefinitions.LowStockThreshold));
        if (lowStockCount > 0)
        {
            insights.Add(new BusinessInsightDto
            {
                Kind = BusinessInsightKind.LowStockRewards,
                Severity = DashboardInsightSeverity.Warning,
                Count = lowStockCount,
            });
        }

        var expiryCutoff = now.AddMinutes(DashboardDefinitions.BuyNowExpiryWarningMinutes);
        var expiringCount = await AsyncExecuter.CountAsync(
            orders.Where(o => o.Status == SmartOfferOrderStatus.Pending
                && o.ReservationExpiresAt > now && o.ReservationExpiresAt <= expiryCutoff));
        if (expiringCount > 0)
        {
            insights.Add(new BusinessInsightDto
            {
                Kind = BusinessInsightKind.BuyNowExpiringSoon,
                Severity = DashboardInsightSeverity.Warning,
                Count = expiringCount,
            });
        }

        var activityWindowStart = now.AddDays(-DashboardDefinitions.ActivityWindowDays);
        var recentFlows = await _metrics.GetHourlyFlowAsync(activityWindowStart, now, platformWide: false);

        var activityByLocalHour = new Dictionary<int, int>();
        foreach (var flow in recentFlows)
        {
            var localHour = SmartOfferTiming.ToLocal(flow.HourUtc, zone.Zone).Minute / 60;
            activityByLocalHour[localHour] = activityByLocalHour.GetValueOrDefault(localHour) + flow.Transactions + flow.BuyNowSales;
        }

        var busiestHours = activityByLocalHour
            .Where(pair => pair.Value > 0)
            .OrderByDescending(pair => pair.Value)
            .Take(DashboardDefinitions.PeakInsightHours)
            .Select(pair => pair.Key)
            .ToList();

        // A deal covers an hour if it's a fixed-price deal (always on while valid), or a time-based stage spans that
        // minute. Only offers inside their validity dates count, since that's what onSale already holds.
        bool IsCovered(int hour) => onSale.Any(offer =>
            offer.Strategy == SmartPricingStrategy.Fixed
            || offer.Stages.Any(stage => stage.StartMinute <= hour * 60 && hour * 60 < stage.EndMinute));

        // busiestHours is already ordered busiest-first, so the first uncovered one is the one worth raising.
        var uncovered = busiestHours.Where(hour => !IsCovered(hour)).ToList();
        if (uncovered.Count > 0)
        {
            insights.Add(new BusinessInsightDto
            {
                Kind = BusinessInsightKind.UncoveredPeakHour,
                Severity = DashboardInsightSeverity.Info,
                Hour = uncovered[0],
            });
        }

        var purchases = recentFlows.Sum(f => f.Transactions);
        var withAmount = recentFlows.Sum(f => f.PurchasesWithAmount);
        var coverage = Percent(withAmount, purchases);
        if (purchases >= DashboardDefinitions.MinPurchasesForCoverageInsight
            && coverage < DashboardDefinitions.LowValueCoveragePercent)
        {
            insights.Add(new BusinessInsightDto
            {
                Kind = BusinessInsightKind.LowValueCoverage,
                Severity = DashboardInsightSeverity.Warning,
                Percent = coverage,
            });
        }

        return insights;
    }

    private async Task<BusinessZone> ResolveZoneAsync()
    {
        // The tenant filter scopes this to the caller's own profile. A business without one (not expected, since
        // registration creates it) falls back to the default zone rather than failing the whole dashboard.
        var profile = await _profileRepository.FirstOrDefaultAsync(p => true);
        var zoneId = profile?.TimeZoneId ?? BusinessProfileConsts.DefaultTimeZoneId;
        return new BusinessZone(zoneId, SmartOfferTiming.ResolveTimeZone(zoneId));
    }

    // Customers with at least one Earn in [startUtc, endUtc).
    private async Task<int> CountEarningMembersAsync(DateTime startUtc, DateTime endUtc)
    {
        var transactions = await _transactionRepository.GetQueryableAsync();
        var earningWallets = transactions
            .Where(t => t.Type == PointsTransactionType.Earn && t.CreationTime >= startUtc && t.CreationTime < endUtc)
            .Select(t => t.WalletId)
            .Distinct();

        return await AsyncExecuter.CountAsync(earningWallets);
    }

    // Of the customers who earned in the window, the ones who had already earned before it started.
    private async Task<int> CountReturningMembersAsync(DateTime startUtc, DateTime endUtc)
    {
        var transactions = await _transactionRepository.GetQueryableAsync();
        var earningWallets = transactions
            .Where(t => t.Type == PointsTransactionType.Earn && t.CreationTime >= startUtc && t.CreationTime < endUtc)
            .Select(t => t.WalletId)
            .Distinct();

        var returning = earningWallets.Where(walletId =>
            transactions.Any(t => t.WalletId == walletId && t.Type == PointsTransactionType.Earn && t.CreationTime < startUtc));

        return await AsyncExecuter.CountAsync(returning);
    }

    private async Task<int> CountNewMembersAsync(DateTime startUtc, DateTime endUtc)
    {
        var memberships = await _membershipRepository.GetQueryableAsync();
        return await AsyncExecuter.CountAsync(
            memberships.Where(m => m.JoinedAt >= startUtc && m.JoinedAt < endUtc));
    }

    private async Task<int> CountLiveOffersAsync(DateTime now)
    {
        var (enabledOffers, _) = await _offerRepository.GetListAsync(enabledOnly: true);
        return enabledOffers.Count(o => o.GetStatus(now) == SmartOfferStatus.Live);
    }

    private static bool IsLapsed(SmartOfferOrderStatus status) =>
        status is SmartOfferOrderStatus.Rejected or SmartOfferOrderStatus.Cancelled or SmartOfferOrderStatus.Expired;

    // Null when there's no base to take a share of, so an empty window doesn't read as 0%.
    private static decimal? Percent(int part, int whole) =>
        whole > 0 ? Math.Round((decimal)part * 100m / whole, 1) : null;

    // The same share as a fraction (0 to 1), for fields that follow the RedemptionRate convention.
    private static decimal? Ratio(int part, int whole) =>
        whole > 0 ? (decimal)part / whole : null;

    private sealed record BusinessZone(string Id, TimeZoneInfo Zone);
}
