using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading;
using System.Threading.Tasks;
using Eksabli.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace Eksabli.Billing;

public class EfCoreTenantSubscriptionRepository : EfCoreRepository<EksabliDbContext, TenantSubscription, Guid>, ITenantSubscriptionRepository
{
    public EfCoreTenantSubscriptionRepository(IDbContextProvider<EksabliDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public async Task<(List<TenantSubscription> Items, int TotalCount)> GetListAsync(
        TenantSubscriptionStatus? status = null,
        Guid? tenantId = null,
        string? sorting = null,
        int skipCount = 0,
        int maxResultCount = int.MaxValue,
        CancellationToken cancellationToken = default)
    {
        var queryable = ApplyFilter(await GetQueryableAsync(), status, tenantId);

        var totalCount = await AsyncExecuter.CountAsync(queryable, GetCancellationToken(cancellationToken));

        var items = await AsyncExecuter.ToListAsync(
            queryable
                .OrderBy(sorting.IsNullOrWhiteSpace() ? "CreationTime desc" : sorting)
                .Skip(skipCount)
                .Take(maxResultCount),
            GetCancellationToken(cancellationToken));

        return (items, totalCount);
    }

    protected virtual IQueryable<TenantSubscription> ApplyFilter(IQueryable<TenantSubscription> query, TenantSubscriptionStatus? status, Guid? tenantId)
    {
        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        if (tenantId.HasValue)
        {
            query = query.Where(x => x.TenantId == tenantId.Value);
        }

        return query;
    }
}
