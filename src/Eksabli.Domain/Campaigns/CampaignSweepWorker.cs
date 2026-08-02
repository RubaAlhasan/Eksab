using System;
using System.Linq;
using System.Threading.Tasks;
using Eksabli.Notifications;
using Eksabli.Wallets;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Volo.Abp.Threading;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace Eksabli.Campaigns;

// Third AsyncPeriodicBackgroundWorkerBase in this repo (mirrors Wallets.PointsExpirationWorker,
// Billing.SubscriptionRenewalWorker). Handles the "scheduled segment sweep" evaluation mode from
// docs/eksabli-loyalty-platform/features/05-campaigns-notifications/README.md — Birthday/WinBack/Vip/
// NewCustomer campaigns. DoublePoints/SpendXGetY are the *other* mode (real-time, inline in
// PosAppService.ComputePointsAsync via ICampaignRulesEngine) and are deliberately not touched here.
public class CampaignSweepWorker : AsyncPeriodicBackgroundWorkerBase
{
    public CampaignSweepWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory)
        : base(timer, serviceScopeFactory)
    {
        Timer.Period = 24 * 60 * 60 * 1000; // daily
    }

    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        var tenantRepository = workerContext.ServiceProvider.GetRequiredService<ITenantRepository>();
        var currentTenant = workerContext.ServiceProvider.GetRequiredService<ICurrentTenant>();
        var unitOfWorkManager = workerContext.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();

        var tenants = await tenantRepository.GetListAsync();

        foreach (var tenant in tenants)
        {
            using var uow = unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
            using (currentTenant.Change(tenant.Id))
            {
                await ProcessTenantAsync(workerContext.ServiceProvider);
            }
            await uow.CompleteAsync();
        }
    }

    private static async Task ProcessTenantAsync(IServiceProvider serviceProvider)
    {
        var campaignRepository = serviceProvider.GetRequiredService<ICampaignRepository>();
        var notificationRepository = serviceProvider.GetRequiredService<INotificationRepository>();
        var walletRepository = serviceProvider.GetRequiredService<IRepository<PointsWallet, Guid>>();
        var transactionRepository = serviceProvider.GetRequiredService<IRepository<PointsTransaction, Guid>>();
        var segmentEvaluator = serviceProvider.GetRequiredService<ICampaignSegmentEvaluator>();
        var backgroundJobManager = serviceProvider.GetRequiredService<IBackgroundJobManager>();
        var guidGenerator = serviceProvider.GetRequiredService<IGuidGenerator>();
        var clock = serviceProvider.GetRequiredService<IClock>();

        var now = clock.Now;

        var (activeCampaigns, _) = await campaignRepository.GetListAsync(status: CampaignStatus.Active);

        // Housekeeping: a campaign whose window closed doesn't stay "Active" forever waiting for staff
        // to notice.
        foreach (var expired in activeCampaigns.Where(c => c.EndDate <= now))
        {
            expired.End();
            await campaignRepository.UpdateAsync(expired);
        }

        var sweepCampaigns = activeCampaigns.Where(c => c.EndDate > now && IsScheduledSweepType(c.Type)).ToList();
        if (sweepCampaigns.Count == 0)
        {
            return;
        }

        var sentToday = await notificationRepository.CountCreatedSinceAsync(now.Date);
        var remainingQuota = NotificationConsts.MaxDailyNotificationsPerTenant - sentToday;

        foreach (var campaign in sweepCampaigns)
        {
            if (remainingQuota <= 0)
            {
                break;
            }

            var rules = CampaignRules.Parse(campaign.RulesJson);
            var targets = await segmentEvaluator.EvaluateAsync(campaign);
            var pointsSource = campaign.Type == CampaignType.Birthday
                ? PointsTransactionSource.Birthday
                : PointsTransactionSource.Campaign;

            foreach (var membership in targets)
            {
                if (remainingQuota <= 0)
                {
                    break;
                }

                if (await notificationRepository.ExistsForCampaignAsync(campaign.Id, membership.Id))
                {
                    continue; // already notified for this campaign — the sweep is idempotent
                }

                if (rules.BonusPoints is > 0)
                {
                    var wallet = await walletRepository.FirstAsync(w => w.MembershipId == membership.Id);
                    var transaction = PointsTransaction.Create(
                        guidGenerator.Create(),
                        wallet.Id,
                        PointsTransactionType.Earn,
                        rules.BonusPoints.Value,
                        pointsSource,
                        referenceId: campaign.Id);
                    await transactionRepository.InsertAsync(transaction);

                    wallet.ApplyTransaction(PointsTransactionType.Earn, rules.BonusPoints.Value);
                    await walletRepository.UpdateAsync(wallet);
                }

                var notification = Notification.Create(
                    guidGenerator.Create(),
                    membership.Id,
                    NotificationChannel.Push,
                    campaign.NameEn,
                    BuildBody(campaign, rules),
                    campaign.Id);
                await notificationRepository.InsertAsync(notification);

                await backgroundJobManager.EnqueueAsync(new NotificationDispatchArgs { NotificationId = notification.Id });

                remainingQuota--;
            }
        }
    }

    private static bool IsScheduledSweepType(CampaignType type) =>
        type is CampaignType.Birthday or CampaignType.WinBack or CampaignType.Vip or CampaignType.NewCustomer;

    private static string BuildBody(Campaign campaign, CampaignRules rules) =>
        rules.BonusPoints is > 0
            ? $"You've earned {rules.BonusPoints} bonus points from {campaign.NameEn}!"
            : $"Check out {campaign.NameEn} — a special offer just for you.";
}
