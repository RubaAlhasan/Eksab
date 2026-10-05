using System;
using System.Threading.Tasks;
using Eksabli.Memberships;
using Eksabli.Notifications;
using Eksabli.Wallets;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;

namespace Eksabli.SmartOffers;

// Points for a collected Smart Deal. A deal is a purchase, so it earns the business's own per-currency rule on the
// amount actually paid (TotalAmount, in the order's currency), recorded as a Purchase row that points back at the order.
// The tier multiplier and campaign multipliers are not applied: a deal is priced by the business already, and stacking
// multipliers on a discounted price would make the deal worth more than the business chose to offer.
public interface ISmartDealPointsAwarder
{
    /// <summary>Awards the points for a collected order and tells the customer. Returns the points awarded (0 when the business has no matching rule).</summary>
    Task<int> AwardForCollectedOrderAsync(SmartOfferOrder order);
}

public class SmartDealPointsAwarder : ISmartDealPointsAwarder, ITransientDependency
{
    private readonly IRepository<PointRule, Guid> _pointRuleRepository;
    private readonly IRepository<PointsWallet, Guid> _walletRepository;
    private readonly IRepository<PointsTransaction, Guid> _transactionRepository;
    private readonly IRepository<Membership, Guid> _membershipRepository;
    private readonly IPointsExpiryPolicy _pointsExpiryPolicy;
    private readonly ITierRecomputeService _tierRecomputeService;
    private readonly INotificationPublisher _notificationPublisher;
    private readonly IGuidGenerator _guidGenerator;

    public SmartDealPointsAwarder(
        IRepository<PointRule, Guid> pointRuleRepository,
        IRepository<PointsWallet, Guid> walletRepository,
        IRepository<PointsTransaction, Guid> transactionRepository,
        IRepository<Membership, Guid> membershipRepository,
        IPointsExpiryPolicy pointsExpiryPolicy,
        ITierRecomputeService tierRecomputeService,
        INotificationPublisher notificationPublisher,
        IGuidGenerator guidGenerator)
    {
        _pointRuleRepository = pointRuleRepository;
        _walletRepository = walletRepository;
        _transactionRepository = transactionRepository;
        _membershipRepository = membershipRepository;
        _pointsExpiryPolicy = pointsExpiryPolicy;
        _tierRecomputeService = tierRecomputeService;
        _notificationPublisher = notificationPublisher;
        _guidGenerator = guidGenerator;
    }

    public async Task<int> AwardForCollectedOrderAsync(SmartOfferOrder order)
    {
        var membership = await _membershipRepository.GetAsync(order.MembershipId);

        var rule = await _pointRuleRepository.FirstOrDefaultAsync(r =>
            r.RuleType == PointRuleType.PerCurrencyUnit && r.Currency == order.Currency);
        var points = rule == null ? 0 : (int)Math.Floor(order.TotalAmount * rule.PointsPerUnit);

        if (points > 0)
        {
            var wallet = await _walletRepository.FirstAsync(w => w.MembershipId == membership.Id);
            var expiresAt = await _pointsExpiryPolicy.GetExpiresAtForEarnAsync();

            var transaction = PointsTransaction.Create(
                _guidGenerator.Create(),
                wallet.Id,
                PointsTransactionType.Earn,
                points,
                PointsTransactionSource.Purchase,
                referenceId: order.Id,
                expiresAt: expiresAt,
                amount: order.TotalAmount,
                currency: order.Currency);
            await _transactionRepository.InsertAsync(transaction);

            wallet.ApplyTransaction(PointsTransactionType.Earn, points);
            await _tierRecomputeService.RecomputeAsync(wallet);
            await _walletRepository.UpdateAsync(wallet);
        }

        var message = points > 0
            ? $"Thanks for collecting your deal. {points} points were added to your balance."
            : "Thanks for collecting your deal.";
        await _notificationPublisher.PublishToUserAsync(
            membership.CustomerId,
            order.TenantId,
            UserNotificationType.Success,
            "Deal collected",
            message,
            category: "smartdeal.collected",
            data: new { tenantId = order.TenantId });

        return points;
    }
}
