using System;
using System.Threading.Tasks;
using Eksabli.Memberships;
using Eksabli.Notifications;
using Eksabli.Wallets;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;

namespace Eksabli.Engagement;

public class ReferralCompletionService : IReferralCompletionService, ITransientDependency
{
    private readonly IReferralRepository _referralRepository;
    private readonly IRepository<Membership, Guid> _membershipRepository;
    private readonly IRepository<PointsWallet, Guid> _walletRepository;
    private readonly IRepository<PointsTransaction, Guid> _transactionRepository;
    private readonly INotificationRepository _notificationRepository;
    private readonly IBackgroundJobManager _backgroundJobManager;
    private readonly IGuidGenerator _guidGenerator;
    private readonly ITierRecomputeService _tierRecomputeService;
    private readonly INotificationPublisher _notificationPublisher;
    private readonly ICurrentTenant _currentTenant;
    private readonly IPointsExpiryPolicy _pointsExpiryPolicy;

    public ReferralCompletionService(
        IReferralRepository referralRepository,
        IRepository<Membership, Guid> membershipRepository,
        IRepository<PointsWallet, Guid> walletRepository,
        IRepository<PointsTransaction, Guid> transactionRepository,
        INotificationRepository notificationRepository,
        IBackgroundJobManager backgroundJobManager,
        IGuidGenerator guidGenerator,
        ITierRecomputeService tierRecomputeService,
        INotificationPublisher notificationPublisher,
        ICurrentTenant currentTenant,
        IPointsExpiryPolicy pointsExpiryPolicy)
    {
        _pointsExpiryPolicy = pointsExpiryPolicy;
        _referralRepository = referralRepository;
        _membershipRepository = membershipRepository;
        _walletRepository = walletRepository;
        _transactionRepository = transactionRepository;
        _notificationRepository = notificationRepository;
        _backgroundJobManager = backgroundJobManager;
        _guidGenerator = guidGenerator;
        _tierRecomputeService = tierRecomputeService;
        _notificationPublisher = notificationPublisher;
        _currentTenant = currentTenant;
    }

    public async Task TryCompleteAsync(Membership refereeMembership, PointsWallet refereeWallet, bool isFirstEarn)
    {
        if (!isFirstEarn)
        {
            return;
        }

        var referral = await _referralRepository.FindPendingByRefereeAsync(refereeMembership.CustomerId);
        if (referral == null)
        {
            return;
        }

        var referrerMembership = await _membershipRepository.GetAsync(referral.ReferrerMembershipId);
        var referrerWallet = await _walletRepository.FirstAsync(w => w.MembershipId == referrerMembership.Id);

        referral.Complete();
        referral.MarkRewarded();
        await _referralRepository.UpdateAsync(referral);

        await AwardBonusAsync(refereeWallet, referral.Id);
        await AwardBonusAsync(referrerWallet, referral.Id);

        await NotifyAsync(refereeMembership, "You've earned a referral bonus for joining!");
        await NotifyAsync(referrerMembership, "Your referral just earned you a bonus — thanks for spreading the word!");
    }

    private async Task AwardBonusAsync(PointsWallet wallet, Guid referralId)
    {
        var transaction = PointsTransaction.Create(
            _guidGenerator.Create(),
            wallet.Id,
            PointsTransactionType.Earn,
            ReferralConsts.BonusPoints,
            PointsTransactionSource.Referral,
            referenceId: referralId,
            expiresAt: await _pointsExpiryPolicy.GetExpiresAtForEarnAsync());
        await _transactionRepository.InsertAsync(transaction);

        wallet.ApplyTransaction(PointsTransactionType.Earn, ReferralConsts.BonusPoints);

        // The bonus moves LifetimeEarned, so CurrentTierId needs re-checking here too — otherwise a
        // referral bonus that crosses a tier threshold wouldn't show up until the wallet's next
        // purchase (see ITierRecomputeService's own comment for why that's not just cosmetic).
        await _tierRecomputeService.RecomputeAsync(wallet);
        await _walletRepository.UpdateAsync(wallet);
    }

    // Writes both the Push-channel delivery record (NotificationSender -> IPushNotificationSender,
    // which has no real provider configured — see that class's own comment) AND the actual in-app inbox
    // row via INotificationPublisher, which the customer's /customer/alerts page reads. Without the
    // latter a referral bonus notification never reached the customer anywhere.
    private async Task NotifyAsync(Membership membership, string body)
    {
        var notification = Notification.Create(
            _guidGenerator.Create(),
            membership.Id,
            NotificationChannel.Push,
            "Referral bonus!",
            body);
        await _notificationRepository.InsertAsync(notification);

        await _backgroundJobManager.EnqueueAsync(new NotificationDispatchArgs { NotificationId = notification.Id });

        await _notificationPublisher.PublishToUserAsync(
            membership.CustomerId,
            _currentTenant.Id,
            UserNotificationType.Success,
            "Referral bonus!",
            body,
            category: "Referral");
    }
}
