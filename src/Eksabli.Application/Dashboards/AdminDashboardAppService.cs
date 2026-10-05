using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Eksabli.Billing;
using Eksabli.BusinessProfiles;
using Eksabli.CustomerProfiles;
using Eksabli.Permissions;
using Eksabli.Platform;
using Eksabli.Rewards;
using Eksabli.Shared;
using Eksabli.SmartOffers;
using Eksabli.Wallets;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Services;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;

namespace Eksabli.Dashboards;

// Admin Portal Dashboard 360. Platform-wide, so it reads every tenant (Disable<IMultiTenant>()) and uses UTC days.
// Two sections are gated separately from the page itself: money (Billing.ManagePlatform) and the support queue
// (SupportTickets.Manage). A caller without them still gets the rest of the dashboard, with those fields left null.
[RemoteService(IsEnabled = false)]
[Authorize(EksabliPermissions.PlatformReports.Default)]
public class AdminDashboardAppService : ApplicationService, IAdminDashboardAppService
{
    private const int RecentActivityCount = 15;

    private readonly DashboardMetricsService _metrics;
    private readonly IDataFilter _dataFilter;
    private readonly IRepository<BusinessProfile, Guid> _profileRepository;
    private readonly IRepository<PointsTransaction, Guid> _transactionRepository;
    private readonly IRepository<CustomerProfile, Guid> _customerProfileRepository;
    private readonly ISmartOfferRepository _offerRepository;
    private readonly IRepository<SmartOffer, Guid> _offerGenericRepository;
    private readonly IRepository<SmartOfferOrder, Guid> _orderRepository;
    private readonly IRepository<Reward, Guid> _rewardRepository;
    private readonly IRepository<TenantSubscription, Guid> _subscriptionRepository;
    private readonly IRepository<SupportTicket, Guid> _ticketRepository;
    private readonly IRepository<Tenant, Guid> _tenantRepository;

    public AdminDashboardAppService(
        DashboardMetricsService metrics,
        IDataFilter dataFilter,
        IRepository<BusinessProfile, Guid> profileRepository,
        IRepository<PointsTransaction, Guid> transactionRepository,
        IRepository<CustomerProfile, Guid> customerProfileRepository,
        ISmartOfferRepository offerRepository,
        IRepository<SmartOffer, Guid> offerGenericRepository,
        IRepository<SmartOfferOrder, Guid> orderRepository,
        IRepository<Reward, Guid> rewardRepository,
        IRepository<TenantSubscription, Guid> subscriptionRepository,
        IRepository<SupportTicket, Guid> ticketRepository,
        IRepository<Tenant, Guid> tenantRepository)
    {
        _metrics = metrics;
        _dataFilter = dataFilter;
        _profileRepository = profileRepository;
        _transactionRepository = transactionRepository;
        _customerProfileRepository = customerProfileRepository;
        _offerRepository = offerRepository;
        _offerGenericRepository = offerGenericRepository;
        _orderRepository = orderRepository;
        _rewardRepository = rewardRepository;
        _subscriptionRepository = subscriptionRepository;
        _ticketRepository = ticketRepository;
        _tenantRepository = tenantRepository;
    }

    public async Task<AdminDashboardSummaryDto> GetSummaryAsync(DashboardRangeDto input)
    {
        var now = Clock.Now;
        var (from, to) = DashboardTimeWindow.Resolve(input, DateOnly.FromDateTime(now));
        var (startUtc, endUtc) = DashboardTimeWindow.ToUtcBounds(from, to, TimeZoneInfo.Utc);
        var canViewRevenue = await AuthorizationService.IsGrantedAsync(EksabliPermissions.Billing.ManagePlatform);

        using (_dataFilter.Disable<IMultiTenant>())
        {
            var profiles = await _profileRepository.GetQueryableAsync();
            var statusCounts = await AsyncExecuter.ToListAsync(
                profiles.GroupBy(p => p.ApprovalStatus).Select(g => new { Status = g.Key, Count = g.Count() }));

            var activeSince = now.AddDays(-DashboardDefinitions.ActivityWindowDays);
            var transactions = await _transactionRepository.GetQueryableAsync();
            var earningTenants = transactions
                .Where(t => t.Type == PointsTransactionType.Earn && t.CreationTime >= activeSince)
                .Select(t => t.TenantId)
                .Distinct();
            var activeBusinesses = await AsyncExecuter.CountAsync(
                profiles.Where(p => p.ApprovalStatus == TenantApprovalStatus.Approved && earningTenants.Contains(p.TenantId)));

            var customers = await _customerProfileRepository.GetQueryableAsync();
            var totalCustomers = await AsyncExecuter.CountAsync(customers);
            var newCustomers = await AsyncExecuter.CountAsync(
                customers.Where(c => c.CreationTime >= startUtc && c.CreationTime < endUtc));

            var totals = FlowTotals.Sum(await _metrics.GetHourlyFlowAsync(startUtc, endUtc, platformWide: true));

            var (offers, _) = await _offerRepository.GetListAsync(enabledOnly: true);

            int CountWithStatus(TenantApprovalStatus status) => statusCounts.FirstOrDefault(x => x.Status == status)?.Count ?? 0;

            return new AdminDashboardSummaryDto
            {
                From = from,
                To = to,
                BusinessesApproved = CountWithStatus(TenantApprovalStatus.Approved),
                BusinessesPending = CountWithStatus(TenantApprovalStatus.Pending),
                BusinessesSuspended = CountWithStatus(TenantApprovalStatus.Suspended),
                ActiveBusinesses = activeBusinesses,
                TotalCustomers = totalCustomers,
                NewCustomers = newCustomers,
                PointsIssued = totals.PointsIssued,
                PointsRedeemed = totals.PointsRedeemed,
                Transactions = totals.Transactions,
                LiveOffers = offers.Count(o => o.GetStatus(now) == SmartOfferStatus.Live),
                BuyNowSales = totals.BuyNowSales,
                RecordedValue = canViewRevenue ? totals.RecordedValueAmounts() : null,
                BuyNowValue = canViewRevenue ? totals.BuyNowValueAmounts() : null,
            };
        }
    }

    public async Task<List<AdminDashboardTrendPointDto>> GetTrendsAsync(DashboardRangeDto input)
    {
        var (from, to) = DashboardTimeWindow.Resolve(input, DateOnly.FromDateTime(Clock.Now));
        var (startUtc, endUtc) = DashboardTimeWindow.ToUtcBounds(from, to, TimeZoneInfo.Utc);
        var canViewRevenue = await AuthorizationService.IsGrantedAsync(EksabliPermissions.Billing.ManagePlatform);

        using (_dataFilter.Disable<IMultiTenant>())
        {
            var flows = await _metrics.GetHourlyFlowAsync(startUtc, endUtc, platformWide: true);
            var totalsByDay = flows
                .GroupBy(f => DateOnly.FromDateTime(f.HourUtc))
                .ToDictionary(g => g.Key, g => FlowTotals.Sum(g));

            var customers = await _customerProfileRepository.GetQueryableAsync();
            var profiles = await _profileRepository.GetQueryableAsync();
            var newCustomersByDay = await CountPerDayAsync(
                customers.Where(c => c.CreationTime >= startUtc && c.CreationTime < endUtc).Select(c => c.CreationTime));
            var newBusinessesByDay = await CountPerDayAsync(
                profiles.Where(p => p.CreationTime >= startUtc && p.CreationTime < endUtc).Select(p => p.CreationTime));

            return DashboardTimeWindow.EachDay(from, to)
                .Select(day =>
                {
                    var totals = totalsByDay.GetValueOrDefault(day) ?? new FlowTotals();
                    return new AdminDashboardTrendPointDto
                    {
                        Date = day,
                        PointsIssued = totals.PointsIssued,
                        PointsRedeemed = totals.PointsRedeemed,
                        Transactions = totals.Transactions,
                        NewCustomers = newCustomersByDay.GetValueOrDefault(day),
                        NewBusinesses = newBusinessesByDay.GetValueOrDefault(day),
                        RecordedValue = canViewRevenue ? totals.RecordedValueAmounts() : null,
                    };
                })
                .ToList();
        }
    }

    public async Task<List<TopBusinessDto>> GetTopBusinessesAsync(DashboardRangeDto input)
    {
        var (from, to) = DashboardTimeWindow.Resolve(input, DateOnly.FromDateTime(Clock.Now));
        var (startUtc, endUtc) = DashboardTimeWindow.ToUtcBounds(from, to, TimeZoneInfo.Utc);

        using (_dataFilter.Disable<IMultiTenant>())
        {
            var transactions = await _transactionRepository.GetQueryableAsync();
            var rows = await AsyncExecuter.ToListAsync(
                transactions
                    .Where(t => t.Type == PointsTransactionType.Earn && t.TenantId != null
                        && t.CreationTime >= startUtc && t.CreationTime < endUtc)
                    .GroupBy(t => t.TenantId)
                    .Select(g => new
                    {
                        TenantId = g.Key,
                        Points = g.Sum(t => t.Points),
                        Transactions = g.Count(t => t.Source == PointsTransactionSource.Purchase),
                    })
                    .OrderByDescending(r => r.Points)
                    .Take(DashboardDefinitions.TopListSize));

            var tenantIds = rows.Select(r => r.TenantId!.Value).ToList();
            var displayNames = (await _profileRepository.GetListAsync(p => p.TenantId != null && tenantIds.Contains(p.TenantId.Value)))
                .ToDictionary(p => p.TenantId!.Value, p => p.DisplayName);
            var tenantNames = await GetTenantNamesAsync(tenantIds);

            return rows.Select(r =>
            {
                var tenantId = r.TenantId!.Value;
                return new TopBusinessDto
                {
                    TenantId = tenantId,
                    Name = displayNames.GetValueOrDefault(tenantId) ?? tenantNames.GetValueOrDefault(tenantId) ?? string.Empty,
                    PointsIssued = r.Points,
                    Transactions = r.Transactions,
                };
            }).ToList();
        }
    }

    public async Task<List<TopOfferDto>> GetTopOffersAsync(DashboardRangeDto input)
    {
        var (from, to) = DashboardTimeWindow.Resolve(input, DateOnly.FromDateTime(Clock.Now));
        var (startUtc, endUtc) = DashboardTimeWindow.ToUtcBounds(from, to, TimeZoneInfo.Utc);
        var canViewRevenue = await AuthorizationService.IsGrantedAsync(EksabliPermissions.Billing.ManagePlatform);

        using (_dataFilter.Disable<IMultiTenant>())
        {
            var orders = await _orderRepository.GetQueryableAsync();
            var rows = await AsyncExecuter.ToListAsync(
                orders
                    .Where(o => o.Status == SmartOfferOrderStatus.Completed
                        && o.CompletedAt != null && o.CompletedAt >= startUtc && o.CompletedAt < endUtc)
                    .GroupBy(o => new { o.SmartOfferId, o.Currency })
                    .Select(g => new
                    {
                        g.Key.SmartOfferId,
                        g.Key.Currency,
                        Count = g.Count(),
                        Amount = g.Sum(o => o.TotalAmount),
                    }));

            var topOffers = rows
                .GroupBy(r => r.SmartOfferId)
                .Select(g => new { OfferId = g.Key, Completed = g.Sum(r => r.Count) })
                .OrderByDescending(x => x.Completed)
                .Take(DashboardDefinitions.TopListSize)
                .ToList();

            var offerIds = topOffers.Select(x => x.OfferId).ToList();
            var offersById = (await _offerGenericRepository.GetListAsync(o => offerIds.Contains(o.Id)))
                .ToDictionary(o => o.Id);
            var tenantNames = await GetTenantNamesAsync(
                offersById.Values.Where(o => o.TenantId != null).Select(o => o.TenantId!.Value).Distinct().ToList());

            return topOffers
                .Where(x => offersById.ContainsKey(x.OfferId))
                .Select(x =>
                {
                    var offer = offersById[x.OfferId];
                    var tenantId = offer.TenantId ?? Guid.Empty;
                    return new TopOfferDto
                    {
                        OfferId = offer.Id,
                        TenantId = tenantId,
                        TenantName = tenantNames.GetValueOrDefault(tenantId) ?? string.Empty,
                        TitleAr = offer.TitleAr,
                        TitleEn = offer.TitleEn,
                        Completed = x.Completed,
                        CompletedValue = canViewRevenue
                            ? FlowTotals.ToAmounts(
                                rows.Where(r => r.SmartOfferId == x.OfferId)
                                    .GroupBy(r => r.Currency)
                                    .ToDictionary(g => g.Key, g => g.Sum(r => r.Amount)))
                            : null,
                    };
                })
                .ToList();
        }
    }

    public async Task<AdminAlertsDto> GetAlertsAsync()
    {
        var canViewBilling = await AuthorizationService.IsGrantedAsync(EksabliPermissions.Billing.ManagePlatform);
        var canViewTickets = await AuthorizationService.IsGrantedAsync(EksabliPermissions.SupportTickets.Manage);

        using (_dataFilter.Disable<IMultiTenant>())
        {
            var profiles = await _profileRepository.GetQueryableAsync();
            var rewards = await _rewardRepository.GetQueryableAsync();

            var alerts = new AdminAlertsDto
            {
                PendingApprovals = await AsyncExecuter.CountAsync(
                    profiles.Where(p => p.ApprovalStatus == TenantApprovalStatus.Pending)),
                SuspendedBusinesses = await AsyncExecuter.CountAsync(
                    profiles.Where(p => p.ApprovalStatus == TenantApprovalStatus.Suspended)),
                LowStockRewards = await AsyncExecuter.CountAsync(
                    rewards.Where(r => r.StockRemaining != null && r.StockRemaining <= DashboardDefinitions.LowStockThreshold)),
            };

            if (canViewBilling)
            {
                var subscriptions = await _subscriptionRepository.GetQueryableAsync();
                alerts.PastDueSubscriptions = await AsyncExecuter.CountAsync(
                    subscriptions.Where(s => s.Status == TenantSubscriptionStatus.PastDue));
            }

            if (canViewTickets)
            {
                var tickets = await _ticketRepository.GetQueryableAsync();
                alerts.OpenSupportTickets = await AsyncExecuter.CountAsync(
                    tickets.Where(t => t.Status == SupportTicketStatus.Open || t.Status == SupportTicketStatus.InProgress));
            }

            return alerts;
        }
    }

    public async Task<List<AdminActivityItemDto>> GetActivityAsync()
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var profiles = await _profileRepository.GetQueryableAsync();
            var customers = await _customerProfileRepository.GetQueryableAsync();
            var tickets = await _ticketRepository.GetQueryableAsync();

            var recentBusinesses = await AsyncExecuter.ToListAsync(
                profiles.OrderByDescending(p => p.CreationTime).Take(RecentActivityCount)
                    .Select(p => new { p.TenantId, p.DisplayName, p.CreationTime }));
            var recentCustomers = await AsyncExecuter.ToListAsync(
                customers.OrderByDescending(c => c.CreationTime).Take(RecentActivityCount)
                    .Select(c => c.CreationTime));
            var recentTickets = await AsyncExecuter.ToListAsync(
                tickets.OrderByDescending(t => t.CreationTime).Take(RecentActivityCount)
                    .Select(t => new { t.Subject, t.CreationTime }));

            var tenantNames = await GetTenantNamesAsync(
                recentBusinesses.Where(b => b.TenantId != null).Select(b => b.TenantId!.Value).ToList());

            var items = recentBusinesses
                .Select(b => new AdminActivityItemDto
                {
                    Kind = AdminActivityKind.BusinessRegistered,
                    OccurredAt = b.CreationTime,
                    Subject = b.DisplayName ?? tenantNames.GetValueOrDefault(b.TenantId ?? Guid.Empty),
                })
                .Concat(recentCustomers.Select(time => new AdminActivityItemDto
                {
                    Kind = AdminActivityKind.CustomerJoined,
                    OccurredAt = time,
                }))
                .Concat(recentTickets.Select(t => new AdminActivityItemDto
                {
                    Kind = AdminActivityKind.SupportTicketOpened,
                    OccurredAt = t.CreationTime,
                    Subject = t.Subject,
                }));

            return items.OrderByDescending(i => i.OccurredAt).Take(RecentActivityCount).ToList();
        }
    }

    // Tenant names come from the Tenant table (the technical identifier), used when a business hasn't set a display name.
    private async Task<Dictionary<Guid, string>> GetTenantNamesAsync(IReadOnlyCollection<Guid> tenantIds)
    {
        if (tenantIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        return (await _tenantRepository.GetListAsync(t => tenantIds.Contains(t.Id)))
            .ToDictionary(t => t.Id, t => t.Name);
    }

    // Counts timestamps per UTC calendar day, grouped in the database. Only days that have rows are returned.
    private async Task<Dictionary<DateOnly, int>> CountPerDayAsync(IQueryable<DateTime> timestamps)
    {
        var rows = await AsyncExecuter.ToListAsync(
            timestamps
                .GroupBy(t => new { t.Year, t.Month, t.Day })
                .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, Count = g.Count() }));

        return rows.ToDictionary(r => new DateOnly(r.Year, r.Month, r.Day), r => r.Count);
    }
}
