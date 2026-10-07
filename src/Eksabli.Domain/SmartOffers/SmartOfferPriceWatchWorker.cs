using System;
using System.Linq;
using System.Threading.Tasks;
using Eksabli.Notifications;
using Eksabli.Shared;
using Eksabli.SmartOffers;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Volo.Abp.Threading;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace Eksabli.SmartOffers;

// Sends the "tell me when it drops" notice. Every few minutes it looks at each watched deal: if the next price change is
// lower than the current price (or the deal returns to sale), and that change is less than an hour away, the customer is
// told the time and price, and the watch is removed.
//
// The hour is deliberate. Far enough ahead that the customer has time to come in, close enough that the notice is not
// about something days away and forgotten by the time it arrives.
public class SmartOfferPriceWatchWorker : AsyncPeriodicBackgroundWorkerBase
{
    private static readonly TimeSpan NoticeWindow = TimeSpan.FromHours(1);

    public SmartOfferPriceWatchWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory)
        : base(timer, serviceScopeFactory)
    {
        Timer.Period = 5 * 60 * 1000; // every 5 minutes
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
                await NotifyWatchedDropsAsync(workerContext.ServiceProvider);
            }
            await uow.CompleteAsync();
        }
    }

    private static async Task NotifyWatchedDropsAsync(IServiceProvider serviceProvider)
    {
        var watchRepository = serviceProvider.GetRequiredService<IRepository<SmartOfferWatch, Guid>>();
        var offerRepository = serviceProvider.GetRequiredService<ISmartOfferRepository>();
        var publisher = serviceProvider.GetRequiredService<INotificationPublisher>();
        var currentTenant = serviceProvider.GetRequiredService<ICurrentTenant>();
        var clock = serviceProvider.GetRequiredService<IClock>();

        var watches = await watchRepository.GetListAsync();
        if (watches.Count == 0)
        {
            return;
        }

        var now = clock.Now;

        foreach (var watch in watches)
        {
            var offer = await offerRepository.FindWithStagesAsync(watch.SmartOfferId);

            // A deal that was switched off or has no price change left has nothing more to tell, so the watch is dropped.
            if (offer == null || !offer.IsEnabled)
            {
                await watchRepository.DeleteAsync(watch);
                continue;
            }

            var change = offer.GetNextChange(now);
            if (change == null)
            {
                await watchRepository.DeleteAsync(watch);
                continue;
            }

            var currentQuote = offer.Quote(now);

            // Only a lower price or a return to sale is worth telling about. A rise, or the deal ending, is not.
            var isDrop = change.Price is decimal next && (currentQuote == null || next < currentQuote.Price);
            if (!isDrop || change.AtUtc - now > NoticeWindow)
            {
                continue; // not a drop, or too early to say; look again on the next run
            }

            var (_, minute) = offer.ToLocal(change.AtUtc);
            var currencyCode = offer.Currency == Currency.Usd ? "USD" : "SYP";
            var title = currentQuote == null ? "Deal back on sale" : "Price drop";
            var message = $"{offer.TitleEn} is {change.Price!.Value:0.00} {currencyCode} from {MinuteOfDay.Format(minute)}.";

            await publisher.PublishToUserAsync(
                watch.CustomerId,
                currentTenant.Id,
                UserNotificationType.Info,
                title,
                message,
                category: "smartdeal.price_drop",
                data: new { tenantId = currentTenant.Id, offerId = offer.Id });

            await watchRepository.DeleteAsync(watch);
        }
    }
}
