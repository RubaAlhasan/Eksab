using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
using Volo.Abp.Threading;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace Eksabli.SmartOffers;

// Safety net for the Buy Now reservation: a Pending order holds stock the customer might never collect, so
// something has to give it back when no one completes or rejects it. Mirrors RedemptionReservationWorker.
//
// Runs every five minutes. An order can also be refused on the spot (SmartOfferOrder.Complete checks the
// lapse), so this worker is about returning stock promptly, not about correctness.
public class SmartOfferOrderExpirationWorker : AsyncPeriodicBackgroundWorkerBase
{
    public SmartOfferOrderExpirationWorker(AbpAsyncTimer timer, IServiceScopeFactory serviceScopeFactory)
        : base(timer, serviceScopeFactory)
    {
        Timer.Period = 5 * 60 * 1000;
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
                await ExpireLapsedOrdersAsync(workerContext.ServiceProvider);
            }

            await uow.CompleteAsync();
        }
    }

    private static async Task ExpireLapsedOrdersAsync(IServiceProvider serviceProvider)
    {
        var orderRepository = serviceProvider.GetRequiredService<IRepository<SmartOfferOrder, Guid>>();
        var inventoryRepository = serviceProvider.GetRequiredService<IRepository<SmartOfferInventory, Guid>>();
        var clock = serviceProvider.GetRequiredService<IClock>();

        var now = clock.Now;

        var lapsed = await orderRepository.GetListAsync(o =>
            o.Status == SmartOfferOrderStatus.Pending && o.ReservationExpiresAt <= now);

        if (lapsed.Count == 0)
        {
            return;
        }

        // Several lapsed orders can share one slot and day, so their stock is returned to each inventory row in one
        // write rather than once per order.
        var slotDays = lapsed
            .GroupBy(o => (o.SlotId, o.ServiceDate))
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var (key, orders) in slotDays)
        {
            var inventory = await inventoryRepository.FirstOrDefaultAsync(i => i.SlotId == key.SlotId && i.ServiceDate == key.ServiceDate);
            foreach (var order in orders)
            {
                order.MarkExpired();
                await orderRepository.UpdateAsync(order);
            }

            if (inventory != null)
            {
                inventory.Release(orders.Sum(o => o.Quantity));
                await inventoryRepository.UpdateAsync(inventory);
            }
        }
    }
}
