using System;
using System.Threading.Tasks;
using Eksabli.Memberships;
using Eksabli.Notifications;
using Eksabli.Wallets;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

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

    public ReferralCompletionService(
        IReferralRepository referralRepository,
        IRepository<Membership, Guid> membershipRepository,
        IRepository<PointsWallet, Guid> walletRepository,
        IRepository<PointsTransaction, Guid> transactionRepository,
        INotificationRepository notificationRepository,
        IBackgroundJobManager backgroundJobManager,
        IGuidGenerator guidGenerator,
        ITierRecomputeService tierRecomputeService)
    {
        _referralRepository = referralRepository;
        _membershipRepository = membershipRepository;
        _walletRepository = walletRepository;
        _transactionRepository = transactionRepository;
        _notificationRepository = notificationRepository;
        _backgroundJobManager = backgroundJobManager;
        _guidGenerator = guidGenerator;
        _tierRecomputeService = tierRecomputeService;
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

        await NotifyAsync(refereeMembership.Id, "You've earned a referral bonus for joining!");
        await NotifyAsync(referrerMembership.Id, "Your referral just earned you a bonus — thanks for spreading the word!");
    }

    private async Task AwardBonusAsync(PointsWallet wallet, Guid referralId)
    {
        var transaction = PointsTransaction.Create(
            _guidGenerator.Create(),
            wallet.Id,
            PointsTransactionType.Earn,
            ReferralConsts.BonusPoints,
            PointsTransactionSource.Referral,
            referenceId: referralId);
        await _transactionRepository.InsertAsync(transaction);

        wallet.ApplyTransaction(PointsTransactionType.Earn, ReferralConsts.BonusPoints);

        // The bonus moves LifetimeEarned, so CurrentTierId needs re-checking here too — otherwise a
        // referral bonus that crosses a tier threshold wouldn't show up until the wallet's next
        // purchase (see ITierRecomputeService's own comment for why that's not just cosmetic).
        await _tierRecomputeService.RecomputeAsync(wallet);
        await _walletRepository.UpdateAsync(wallet);
    }

    private async Task NotifyAsync(Guid membershipId, string body)
    {
        var notification = Notification.Create(
            _guidGenerator.Create(),
            membershipId,
            NotificationChannel.Push,
            "Referral bonus!",
            body);
        await _notificationRepository.InsertAsync(notification);

        await _backgroundJobManager.EnqueueAsync(new NotificationDispatchArgs { NotificationId = notification.Id });
    }
}
