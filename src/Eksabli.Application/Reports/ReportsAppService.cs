using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Eksabli.Branches;
using Eksabli.Campaigns;
using Eksabli.CustomerProfiles;
using Eksabli.EmployeeAssignments;
using Eksabli.Memberships;
using Eksabli.Notifications;
using Eksabli.Rewards;
using Eksabli.Shared;
using Eksabli.SmartOffers;
using Eksabli.Wallets;
using Microsoft.Extensions.Caching.Distributed;
using MiniExcelLibs;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Caching;
using Volo.Abp.Content;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;

namespace Eksabli.Reports;

[RemoteService(IsEnabled = false)]
public class ReportsAppService : ApplicationService, IReportsAppService
{
    // Presentation tuning, not a domain rule — how many units left before a reward shows up on the
    // dashboard home as "running low."
    private const int LowStockThreshold = 10;

    // Same cap as the counter's history: each row resolves a customer, so an unbounded page would fan out.
    private const int MaxSalesPageSize = 50;

    private readonly IRepository<Membership, Guid> _membershipRepository;
    private readonly IRepository<PointsWallet, Guid> _walletRepository;
    private readonly IPointsTransactionRepository _transactionRepository;
    private readonly IRepository<Wallets.Tier, Guid> _tierRepository;
    private readonly IRepository<Campaign, Guid> _campaignRepository;
    private readonly IRepository<Reward, Guid> _rewardRepository;
    private readonly IRepository<Coupon, Guid> _couponRepository;
    private readonly IRepository<Branch, Guid> _branchRepository;
    private readonly IRepository<EmployeeAssignment, Guid> _employeeAssignmentRepository;
    private readonly INotificationRepository _notificationRepository;
    private readonly IRepository<Notification, Guid> _notificationGenericRepository;
    private readonly IRepository<CustomerProfile, Guid> _customerProfileRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IDistributedCache<TransactionsExcelDownloadTokenCacheItem, string> _excelDownloadTokenCache;
    private readonly TransactionListItemBuilder _transactionListItemBuilder;
    private readonly IRepository<SmartOfferOrder, Guid> _smartOfferOrderRepository;
    private readonly IRepository<IdentityUser, Guid> _identityUserRepository;
    private readonly IRepository<SmartOffer, Guid> _smartOfferRepository;
    private readonly IDistributedCache<SmartDealSalesExcelDownloadTokenCacheItem, string> _smartDealSalesExcelTokenCache;

    public ReportsAppService(
        IRepository<Membership, Guid> membershipRepository,
        IRepository<PointsWallet, Guid> walletRepository,
        IPointsTransactionRepository transactionRepository,
        IRepository<Wallets.Tier, Guid> tierRepository,
        IRepository<Campaign, Guid> campaignRepository,
        IRepository<Reward, Guid> rewardRepository,
        IRepository<Coupon, Guid> couponRepository,
        IRepository<Branch, Guid> branchRepository,
        IRepository<EmployeeAssignment, Guid> employeeAssignmentRepository,
        INotificationRepository notificationRepository,
        IRepository<Notification, Guid> notificationGenericRepository,
        IRepository<CustomerProfile, Guid> customerProfileRepository,
        ICurrentTenant currentTenant,
        IDistributedCache<TransactionsExcelDownloadTokenCacheItem, string> excelDownloadTokenCache,
        TransactionListItemBuilder transactionListItemBuilder,
        IRepository<SmartOfferOrder, Guid> smartOfferOrderRepository,
        IRepository<IdentityUser, Guid> identityUserRepository,
        IRepository<SmartOffer, Guid> smartOfferRepository,
        IDistributedCache<SmartDealSalesExcelDownloadTokenCacheItem, string> smartDealSalesExcelTokenCache)
    {
        _smartOfferRepository = smartOfferRepository;
        _smartDealSalesExcelTokenCache = smartDealSalesExcelTokenCache;
        _membershipRepository = membershipRepository;
        _walletRepository = walletRepository;
        _transactionRepository = transactionRepository;
        _tierRepository = tierRepository;
        _campaignRepository = campaignRepository;
        _rewardRepository = rewardRepository;
        _couponRepository = couponRepository;
        _branchRepository = branchRepository;
        _employeeAssignmentRepository = employeeAssignmentRepository;
        _notificationRepository = notificationRepository;
        _notificationGenericRepository = notificationGenericRepository;
        _customerProfileRepository = customerProfileRepository;
        _currentTenant = currentTenant;
        _excelDownloadTokenCache = excelDownloadTokenCache;
        _transactionListItemBuilder = transactionListItemBuilder;
        _smartOfferOrderRepository = smartOfferOrderRepository;
        _identityUserRepository = identityUserRepository;
    }

    public async Task<DashboardHomeDto> GetDashboardHomeAsync()
    {
        var last30Days = Clock.Now.AddDays(-30);

        var earnTransactions = await _transactionRepository.GetListAsync(t =>
            t.Type == PointsTransactionType.Earn && t.CreationTime >= last30Days);
        var redeemTransactions = await _transactionRepository.GetListAsync(t =>
            t.Type == PointsTransactionType.Redeem && t.CreationTime >= last30Days);

        var activeMemberCount = earnTransactions.Select(t => t.WalletId).Distinct().Count();
        var activeCampaignCount = await _campaignRepository.CountAsync(c => c.Status == CampaignStatus.Active);

        var lowStockRewards = await _rewardRepository.GetListAsync(r =>
            r.StockRemaining != null && r.StockRemaining <= LowStockThreshold);

        return new DashboardHomeDto
        {
            ActiveMemberCount = activeMemberCount,
            PointsIssuedLast30Days = earnTransactions.Sum(t => t.Points),
            PointsRedeemedLast30Days = -redeemTransactions.Sum(t => t.Points), // Redeem points are stored negative
            ActiveCampaignCount = (int)activeCampaignCount,
            LowStockRewards = lowStockRewards.Select(r => new LowStockRewardDto
            {
                Id = r.Id,
                NameAr = r.NameAr,
                NameEn = r.NameEn,
                StockRemaining = r.StockRemaining!.Value
            }).ToList()
        };
    }

    public async Task<List<MemberGrowthPointDto>> GetMemberGrowthAsync(ReportPeriodDto input)
    {
        var memberships = await _membershipRepository.GetListAsync(m => m.JoinedAt >= input.From && m.JoinedAt <= input.To);

        return memberships
            .GroupBy(m => m.JoinedAt.Date)
            .Select(g => new MemberGrowthPointDto { Date = g.Key, NewMembers = g.Count() })
            .OrderBy(p => p.Date)
            .ToList();
    }

    public async Task<RedemptionRateReportDto> GetRedemptionRateAsync(ReportPeriodDto input)
    {
        var earned = (await _transactionRepository.GetListAsync(t =>
            t.Type == PointsTransactionType.Earn && t.CreationTime >= input.From && t.CreationTime <= input.To)).Sum(t => t.Points);
        var redeemed = -(await _transactionRepository.GetListAsync(t =>
            t.Type == PointsTransactionType.Redeem && t.CreationTime >= input.From && t.CreationTime <= input.To)).Sum(t => t.Points);

        return new RedemptionRateReportDto
        {
            EarnedPoints = earned,
            RedeemedPoints = redeemed,
            RedemptionRate = earned > 0 ? (decimal)redeemed / earned : 0m
        };
    }

    public async Task<List<BranchComparisonDto>> GetBranchComparisonAsync(ReportPeriodDto input)
    {
        var coupons = await _couponRepository.GetListAsync(c =>
            c.Status == CouponStatus.Redeemed &&
            c.RedeemedBranchId != null &&
            c.RedeemedAt >= input.From && c.RedeemedAt <= input.To);

        var grouped = coupons
            .GroupBy(c => c.RedeemedBranchId!.Value)
            .Select(g => new { BranchId = g.Key, Count = g.Count() })
            .ToList();

        var branchIds = grouped.Select(g => g.BranchId).ToList();
        var branchNameLookup = (await _branchRepository.GetListAsync(b => branchIds.Contains(b.Id)))
            .ToDictionary(b => b.Id, b => b.Name);

        return grouped.Select(g => new BranchComparisonDto
        {
            BranchId = g.BranchId,
            BranchName = branchNameLookup.GetValueOrDefault(g.BranchId) ?? string.Empty,
            RedemptionCount = g.Count
        }).ToList();
    }

    public async Task<CustomerSegmentReportDto> GetCustomerSegmentsAsync()
    {
        var now = Clock.Now;
        var last30Days = now.AddDays(-30);
        var last90Days = now.AddDays(-90);

        var memberships = await _membershipRepository.GetListAsync(m => m.Status == MembershipStatus.Active);
        var membershipIds = memberships.Select(m => m.Id).ToList();

        var walletIdByMembershipId = (await _walletRepository.GetListAsync(w => membershipIds.Contains(w.MembershipId)))
            .ToDictionary(w => w.MembershipId, w => w.Id);
        var walletIds = walletIdByMembershipId.Values.ToList();

        var lastEarnByWalletId = (await _transactionRepository.GetListAsync(t =>
                walletIds.Contains(t.WalletId) && t.Type == PointsTransactionType.Earn && t.CreationTime >= last90Days))
            .GroupBy(t => t.WalletId)
            .ToDictionary(g => g.Key, g => g.Max(t => t.CreationTime));

        var result = new CustomerSegmentReportDto();

        foreach (var membership in memberships)
        {
            if (membership.JoinedAt >= last30Days)
            {
                result.New++;
                continue;
            }

            if (!walletIdByMembershipId.TryGetValue(membership.Id, out var walletId) ||
                !lastEarnByWalletId.TryGetValue(walletId, out var lastEarnAt))
            {
                result.Churned++;
                continue;
            }

            if (lastEarnAt >= last30Days)
            {
                result.Active++;
            }
            else
            {
                result.AtRisk++;
            }
        }

        return result;
    }

    public async Task<List<TierDistributionDto>> GetTierDistributionAsync()
    {
        var wallets = await _walletRepository.GetListAsync();

        var grouped = wallets
            .GroupBy(w => w.CurrentTierId)
            .Select(g => new { TierId = g.Key, Count = g.Count() })
            .ToList();

        var tierIds = grouped.Where(g => g.TierId.HasValue).Select(g => g.TierId!.Value).ToList();
        var tierNameLookup = (await _tierRepository.GetListAsync(t => tierIds.Contains(t.Id)))
            .ToDictionary(t => t.Id, t => t.Name);

        return grouped.Select(g => new TierDistributionDto
        {
            TierId = g.TierId,
            TierName = g.TierId.HasValue ? tierNameLookup.GetValueOrDefault(g.TierId.Value) : null,
            MemberCount = g.Count
        }).ToList();
    }

    public async Task<List<TopCustomerDto>> GetTopCustomersAsync(int count = 10)
    {
        var wallets = (await _walletRepository.GetListAsync())
            .OrderByDescending(w => w.LifetimeEarned)
            .Take(count)
            .ToList();

        var membershipIds = wallets.Select(w => w.MembershipId).ToList();
        var membershipLookup = (await _membershipRepository.GetListAsync(m => membershipIds.Contains(m.Id)))
            .ToDictionary(m => m.Id);

        var customerIds = membershipLookup.Values.Select(m => m.CustomerId).Distinct().ToList();
        Dictionary<Guid, CustomerProfile> profileLookup;
        using (_currentTenant.Change(null)) // CustomerProfile is Host-realm
        {
            profileLookup = (await _customerProfileRepository.GetListAsync(p => customerIds.Contains(p.UserId)))
                .ToDictionary(p => p.UserId);
        }

        return wallets.Select(w =>
        {
            membershipLookup.TryGetValue(w.MembershipId, out var membership);
            var customerId = membership?.CustomerId ?? Guid.Empty;
            profileLookup.TryGetValue(customerId, out var profile);

            return new TopCustomerDto
            {
                MembershipId = w.MembershipId,
                CustomerId = customerId,
                LifetimeEarned = w.LifetimeEarned,
                FirstName = profile?.FirstName,
                LastName = profile?.LastName
            };
        }).ToList();
    }

    public async Task<CampaignPerformanceDto> GetCampaignPerformanceAsync(Guid campaignId)
    {
        await _campaignRepository.GetAsync(campaignId); // 404s if not this tenant's campaign

        var (notifications, _) = await _notificationRepository.GetListAsync(campaignId: campaignId, maxResultCount: int.MaxValue);

        var bonusTransactions = await _transactionRepository.GetListAsync(t =>
            t.ReferenceId == campaignId &&
            (t.Source == PointsTransactionSource.Campaign || t.Source == PointsTransactionSource.Birthday));

        return new CampaignPerformanceDto
        {
            CampaignId = campaignId,
            NotificationsSent = notifications.Count(n => n.Status == NotificationStatus.Sent),
            NotificationsQueued = notifications.Count(n => n.Status == NotificationStatus.Queued),
            NotificationsFailed = notifications.Count(n => n.Status == NotificationStatus.Failed),
            BonusPointsAwarded = bonusTransactions.Sum(t => t.Points),
            MembershipsRewarded = bonusTransactions.Select(t => t.WalletId).Distinct().Count()
        };
    }

    public async Task<List<NotificationDeliveryRateDto>> GetNotificationDeliveryRatesAsync(ReportPeriodDto input)
    {
        var notifications = await _notificationGenericRepository.GetListAsync(n =>
            n.CreationTime >= input.From && n.CreationTime <= input.To);

        return notifications
            .GroupBy(n => n.Channel)
            .Select(g =>
            {
                var sent = g.Count(n => n.Status == NotificationStatus.Sent);
                var failed = g.Count(n => n.Status == NotificationStatus.Failed);
                var attempts = sent + failed;

                return new NotificationDeliveryRateDto
                {
                    Channel = g.Key,
                    Sent = sent,
                    Failed = failed,
                    Queued = g.Count(n => n.Status == NotificationStatus.Queued),
                    DeliveryRate = attempts > 0 ? (decimal)sent / attempts : 0m
                };
            })
            .ToList();
    }

    public async Task<DownloadTokenResultDto> GetTransactionsDownloadTokenAsync()
    {
        var token = GuidGenerator.Create().ToString("N");

        await _excelDownloadTokenCache.SetAsync(
            token,
            new TransactionsExcelDownloadTokenCacheItem { Token = token },
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) });

        return new DownloadTokenResultDto { Token = token };
    }

    public async Task<IRemoteStreamContent> GetTransactionsAsExcelFileAsync(TransactionsExcelDownloadDto input)
    {
        var downloadToken = await _excelDownloadTokenCache.GetAsync(input.DownloadToken);
        if (downloadToken == null || input.DownloadToken != downloadToken.Token)
        {
            throw new AbpAuthorizationException("Invalid download token: " + input.DownloadToken);
        }

        var transactions = await _transactionRepository.GetListAsync(t => t.CreationTime >= input.From && t.CreationTime <= input.To);

        var walletIds = transactions.Select(t => t.WalletId).Distinct().ToList();
        var walletToMembership = (await _walletRepository.GetListAsync(w => walletIds.Contains(w.Id)))
            .ToDictionary(w => w.Id, w => w.MembershipId);

        var membershipIds = walletToMembership.Values.Distinct().ToList();
        var membershipToCustomer = (await _membershipRepository.GetListAsync(m => membershipIds.Contains(m.Id)))
            .ToDictionary(m => m.Id, m => m.CustomerId);

        Dictionary<Guid, CustomerProfile> profileLookup;
        using (_currentTenant.Change(null))
        {
            var customerIds = membershipToCustomer.Values.Distinct().ToList();
            profileLookup = (await _customerProfileRepository.GetListAsync(p => customerIds.Contains(p.UserId)))
                .ToDictionary(p => p.UserId);
        }

        var rows = transactions.OrderByDescending(t => t.CreationTime).Select(t =>
        {
            CustomerProfile? profile = null;
            if (walletToMembership.TryGetValue(t.WalletId, out var membershipId) &&
                membershipToCustomer.TryGetValue(membershipId, out var customerId))
            {
                profileLookup.TryGetValue(customerId, out profile);
            }

            return new TransactionExcelDto
            {
                CreationTime = t.CreationTime,
                CustomerFirstName = profile?.FirstName,
                CustomerLastName = profile?.LastName,
                Type = t.Type,
                Points = t.Points,
                Source = t.Source,
                Reason = t.Reason,
                Amount = t.Amount,
                Currency = t.Currency
            };
        });

        var memoryStream = new MemoryStream();
        await memoryStream.SaveAsAsync(rows);
        memoryStream.Seek(0, SeekOrigin.Begin);

        return new RemoteStreamContent(
            memoryStream,
            "Transactions.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
        );
    }

    // Business Portal > Transactions live ledger — the paged/filterable table the prototype's
    // transactions.html shows. Unlike the Excel export above (a bulk dump, one date-bounded read is
    // fine) or MembershipAppService's "load all, filter in memory" shape (bounded by customer count),
    // PointsTransaction is an append-only ledger that grows forever — see the entity's own comment — so
    // this must never materialize the whole matched set. Filtering, ordering, and paging all happen at
    // the database, via IPointsTransactionRepository.GetListAsync (same "filter/sort/page at the
    // database" shape as ICouponRepository/EfCoreCouponRepository). Only Branch/Customer/Staff *display*
    // data is resolved afterwards, and only for the current page's rows (bounded by page size).
    public async Task<PagedResultDto<TransactionListItemDto>> GetTransactionsListAsync(TransactionFilterDto input)
    {
        // PointsTransaction carries no branch of its own — derive it from the staff member who created
        // the row (CreatedByEmployeeId, the same soft reference into EmployeeAssignment.UserId space
        // documented on the entity itself). Resolve BranchId -> matching staff ids *before* the main
        // query so the repository can still push the derived filter into the same database query as
        // everything else — EmployeeAssignment is small and staff-bounded, cheap to fully filter this way.
        ICollection<Guid>? staffIdsForBranch = null;
        if (input.BranchId.HasValue)
        {
            staffIdsForBranch = (await _employeeAssignmentRepository.GetListAsync(e => e.BranchId == input.BranchId))
                .Select(e => e.UserId)
                .ToList();
        }

        // Same "resolve to a real id before the main query" shape as BranchId above — PointsTransaction
        // has no MembershipId of its own either, only WalletId. A membership with no wallet can't
        // happen by construction (MembershipAppService.JoinAsync creates both together), but fail closed
        // (empty result) rather than silently returning every transaction if it ever did.
        Guid? walletIdForMembership = null;
        if (input.MembershipId.HasValue)
        {
            var wallet = await _walletRepository.FirstOrDefaultAsync(w => w.MembershipId == input.MembershipId.Value);
            if (wallet == null)
            {
                return new PagedResultDto<TransactionListItemDto>(0, new List<TransactionListItemDto>());
            }
            walletIdForMembership = wallet.Id;
        }

        var (page, totalCount) = await _transactionRepository.GetListAsync(
            type: input.Type,
            createdByEmployeeId: input.StaffId,
            createdByEmployeeIds: staffIdsForBranch,
            walletId: walletIdForMembership,
            from: input.From,
            to: input.To,
            skipCount: input.SkipCount,
            maxResultCount: input.MaxResultCount);

        // totalCount/paging above is still counted in raw ledger rows, not the grouped-by-process rows
        // TransactionListItemBuilder returns — a batch of up to 4 rows always lands together in this
        // page-sized window at the current page size (10), and a page showing slightly fewer than 10
        // rows once purchases with bonuses collapse is a fine trade-off for not having to make ledger
        // pagination itself batch-aware.
        var items = await _transactionListItemBuilder.BuildAsync(page);

        return new PagedResultDto<TransactionListItemDto>(totalCount, items);
    }

    // Transactions page > "Smart deal sales" tab. Reads SmartOfferOrder directly, not the points ledger: a completed
    // Buy Now sale writes no PointsTransaction rows (see SmartOfferOrder.Complete), so the ledger above cannot show it.
    // Amounts stay in the deal's own currency and points never enter this list. Always newest completed sale first.
    public async Task<PagedResultDto<SmartDealSaleDto>> GetSmartDealSalesAsync(SmartDealSaleFilterDto input)
    {
        var sales = await BuildSmartDealSalesQueryAsync(input.BranchId, input.StaffId, input.From, input.To, input.Search);
        if (input.MembershipId.HasValue)
        {
            sales = sales.Where(o => o.MembershipId == input.MembershipId.Value);
        }

        var total = await AsyncExecuter.CountAsync(sales);
        var orders = await AsyncExecuter.ToListAsync(
            sales
                .OrderByDescending(o => o.CompletedAt)
                .Skip(input.SkipCount)
                .Take(Math.Min(input.MaxResultCount, MaxSalesPageSize)));

        return new PagedResultDto<SmartDealSaleDto>(total, await ToSmartDealSaleDtosAsync(orders));
    }

    // Excel export for the same "Smart deal sales" tab. Same token gate as the points export: a short-lived token from the
    // authorized call, then the anonymous file call that redeems it. The file holds every sale the filters match, with no
    // page limit, so it always agrees with what the table would show across all pages.
    public async Task<DownloadTokenResultDto> GetSmartDealSalesDownloadTokenAsync()
    {
        var token = GuidGenerator.Create().ToString("N");

        await _smartDealSalesExcelTokenCache.SetAsync(
            token,
            new SmartDealSalesExcelDownloadTokenCacheItem { Token = token, TenantId = _currentTenant.Id },
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) });

        return new DownloadTokenResultDto { Token = token };
    }

    public async Task<IRemoteStreamContent> GetSmartDealSalesAsExcelFileAsync(SmartDealSalesExcelDownloadDto input)
    {
        var downloadToken = await _smartDealSalesExcelTokenCache.GetAsync(input.DownloadToken);
        if (downloadToken == null || input.DownloadToken != downloadToken.Token || downloadToken.TenantId != _currentTenant.Id)
        {
            throw new AbpAuthorizationException("Invalid download token: " + input.DownloadToken);
        }

        var sales = await BuildSmartDealSalesQueryAsync(input.BranchId, input.StaffId, input.From, input.To, input.Search);
        var orders = await AsyncExecuter.ToListAsync(sales.OrderByDescending(o => o.CompletedAt));
        var sheet = (await ToSmartDealSaleDtosAsync(orders)).Select(sale => new SmartDealSaleExcelDto
        {
            CompletedAt = sale.CompletedAt,
            DealId = sale.SmartOfferId,
            Code = sale.Code,
            OfferTitleEn = sale.OfferTitleEn,
            OfferTitleAr = sale.OfferTitleAr,
            Quantity = sale.Quantity,
            UnitPrice = sale.UnitPrice,
            BasePrice = sale.BasePrice,
            TotalAmount = sale.TotalAmount,
            CurrencyCode = sale.Currency == Currency.Usd ? "USD" : "SYP",
            ServiceDate = sale.ServiceDate.ToDateTime(TimeOnly.MinValue),
            BranchName = sale.BranchName,
            StaffEmail = sale.StaffEmail,
            CustomerName = $"{sale.CustomerFirstName} {sale.CustomerLastName}".Trim(),
        }).ToList();

        var memoryStream = new MemoryStream();
        await memoryStream.SaveAsAsync(sheet);
        memoryStream.Seek(0, SeekOrigin.Begin);

        return new RemoteStreamContent(
            memoryStream,
            "SmartDealSales.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
        );
    }

    // The completed-sale filter used by both the table and the export, so the file always matches what the table shows.
    private async Task<IQueryable<SmartOfferOrder>> BuildSmartDealSalesQueryAsync(Guid? branchId, Guid? staffId, DateTime? from, DateTime? to, string? search)
    {
        var sales = (await _smartOfferOrderRepository.GetQueryableAsync())
            .Where(o => o.Status == SmartOfferOrderStatus.Completed);

        // A GUID is a Deal ID: every sale of that deal. Anything else is a sale code, typed the way the counter accepts it
        // (case, spaces and hyphens ignored).
        var term = search?.Trim();
        if (!string.IsNullOrEmpty(term))
        {
            if (Guid.TryParse(term, out var dealId))
            {
                sales = sales.Where(o => o.SmartOfferId == dealId);
            }
            else
            {
                var code = term.Replace(" ", string.Empty).Replace("-", string.Empty).ToUpperInvariant();
                sales = sales.Where(o => o.Code == code);
            }
        }

        if (branchId.HasValue)
        {
            sales = sales.Where(o => o.CompletedBranchId == branchId);
        }

        if (staffId.HasValue)
        {
            sales = sales.Where(o => o.CompletedByEmployeeId == staffId);
        }

        if (from.HasValue)
        {
            sales = sales.Where(o => o.CompletedAt >= from);
        }

        if (to.HasValue)
        {
            sales = sales.Where(o => o.CompletedAt <= to);
        }

        return sales;
    }

    // Turns a page (or the whole export set) of completed orders into sale rows. Each lookup runs once per table for
    // exactly these orders' ids, as TransactionListItemBuilder does.
    private async Task<List<SmartDealSaleDto>> ToSmartDealSaleDtosAsync(List<SmartOfferOrder> orders)
    {
        var branchIds = orders.Where(o => o.CompletedBranchId.HasValue).Select(o => o.CompletedBranchId!.Value).Distinct().ToList();
        var branchNameById = branchIds.Count == 0
            ? new Dictionary<Guid, string>()
            : (await _branchRepository.GetListAsync(b => branchIds.Contains(b.Id))).ToDictionary(b => b.Id, b => b.Name);

        var staffIds = orders.Where(o => o.CompletedByEmployeeId.HasValue).Select(o => o.CompletedByEmployeeId!.Value).Distinct().ToList();
        var staffEmailById = staffIds.Count == 0
            ? new Dictionary<Guid, string>()
            : (await _identityUserRepository.GetListAsync(u => staffIds.Contains(u.Id))).ToDictionary(u => u.Id, u => u.Email);

        var offerIds = orders.Select(o => o.SmartOfferId).Distinct().ToList();
        var offersById = offerIds.Count == 0
            ? new Dictionary<Guid, SmartOffer>()
            : (await _smartOfferRepository.GetListAsync(o => offerIds.Contains(o.Id))).ToDictionary(o => o.Id);

        var membershipIds = orders.Select(o => o.MembershipId).Distinct().ToList();
        var customerIdByMembership = (await _membershipRepository.GetListAsync(m => membershipIds.Contains(m.Id)))
            .ToDictionary(m => m.Id, m => m.CustomerId);
        var customerIds = customerIdByMembership.Values.Distinct().ToList();

        List<CustomerProfile> profiles;
        using (_currentTenant.Change(null)) // CustomerProfile is Host-realm, as in GetTransactionsListAsync.
        {
            profiles = await _customerProfileRepository.GetListAsync(p => customerIds.Contains(p.UserId));
        }

        return orders.Select(order =>
        {
            var dto = ObjectMapper.Map<SmartOfferOrder, SmartDealSaleDto>(order);

            if (offersById.TryGetValue(order.SmartOfferId, out var offer))
            {
                dto.OfferDescriptionAr = offer.DescriptionAr;
                dto.OfferDescriptionEn = offer.DescriptionEn;
            }

            if (order.CompletedBranchId.HasValue)
            {
                dto.BranchName = branchNameById.GetValueOrDefault(order.CompletedBranchId.Value);
            }

            if (order.CompletedByEmployeeId.HasValue)
            {
                dto.StaffEmail = staffEmailById.GetValueOrDefault(order.CompletedByEmployeeId.Value);
            }

            if (customerIdByMembership.TryGetValue(order.MembershipId, out var customerId))
            {
                var profile = profiles.FirstOrDefault(p => p.UserId == customerId);
                dto.CustomerFirstName = profile?.FirstName;
                dto.CustomerLastName = profile?.LastName;
            }

            return dto;
        }).ToList();
    }
}
