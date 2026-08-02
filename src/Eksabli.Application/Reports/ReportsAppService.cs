using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Eksabli.Branches;
using Eksabli.Campaigns;
using Eksabli.CustomerProfiles;
using Eksabli.Memberships;
using Eksabli.Notifications;
using Eksabli.Rewards;
using Eksabli.Shared;
using Eksabli.Wallets;
using Microsoft.Extensions.Caching.Distributed;
using MiniExcelLibs;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Caching;
using Volo.Abp.Content;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace Eksabli.Reports;

public class ReportsAppService : ApplicationService, IReportsAppService
{
    // Presentation tuning, not a domain rule — how many units left before a reward shows up on the
    // dashboard home as "running low."
    private const int LowStockThreshold = 10;

    private readonly IRepository<Membership, Guid> _membershipRepository;
    private readonly IRepository<PointsWallet, Guid> _walletRepository;
    private readonly IRepository<PointsTransaction, Guid> _transactionRepository;
    private readonly IRepository<Wallets.Tier, Guid> _tierRepository;
    private readonly IRepository<Campaign, Guid> _campaignRepository;
    private readonly IRepository<Reward, Guid> _rewardRepository;
    private readonly IRepository<Coupon, Guid> _couponRepository;
    private readonly IRepository<Branch, Guid> _branchRepository;
    private readonly INotificationRepository _notificationRepository;
    private readonly IRepository<Notification, Guid> _notificationGenericRepository;
    private readonly IRepository<CustomerProfile, Guid> _customerProfileRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IDistributedCache<TransactionsExcelDownloadTokenCacheItem, string> _excelDownloadTokenCache;

    public ReportsAppService(
        IRepository<Membership, Guid> membershipRepository,
        IRepository<PointsWallet, Guid> walletRepository,
        IRepository<PointsTransaction, Guid> transactionRepository,
        IRepository<Wallets.Tier, Guid> tierRepository,
        IRepository<Campaign, Guid> campaignRepository,
        IRepository<Reward, Guid> rewardRepository,
        IRepository<Coupon, Guid> couponRepository,
        IRepository<Branch, Guid> branchRepository,
        INotificationRepository notificationRepository,
        IRepository<Notification, Guid> notificationGenericRepository,
        IRepository<CustomerProfile, Guid> customerProfileRepository,
        ICurrentTenant currentTenant,
        IDistributedCache<TransactionsExcelDownloadTokenCacheItem, string> excelDownloadTokenCache)
    {
        _membershipRepository = membershipRepository;
        _walletRepository = walletRepository;
        _transactionRepository = transactionRepository;
        _tierRepository = tierRepository;
        _campaignRepository = campaignRepository;
        _rewardRepository = rewardRepository;
        _couponRepository = couponRepository;
        _branchRepository = branchRepository;
        _notificationRepository = notificationRepository;
        _notificationGenericRepository = notificationGenericRepository;
        _customerProfileRepository = customerProfileRepository;
        _currentTenant = currentTenant;
        _excelDownloadTokenCache = excelDownloadTokenCache;
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
                Reason = t.Reason
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
}
