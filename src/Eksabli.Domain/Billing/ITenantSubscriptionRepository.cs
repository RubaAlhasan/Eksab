using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace Eksabli.Billing;

public interface ITenantSubscriptionRepository : IRepository<TenantSubscription, Guid>
{
    Task<(List<TenantSubscription> Items, int TotalCount)> GetListAsync(
        TenantSubscriptionStatus? status = null,
        Guid? tenantId = null,
        string? sorting = null,
        int skipCount = 0,
        int maxResultCount = int.MaxValue,
        CancellationToken cancellationToken = default);
}
