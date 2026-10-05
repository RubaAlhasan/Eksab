using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading;
using System.Threading.Tasks;
using Eksabli.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.EntityFrameworkCore;

namespace Eksabli.SmartOffers;

public class EfCoreSmartOfferRepository : EfCoreRepository<EksabliDbContext, SmartOffer, Guid>, ISmartOfferRepository
{
    public EfCoreSmartOfferRepository(IDbContextProvider<EksabliDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public async Task<(List<SmartOffer> Items, int TotalCount)> GetListAsync(
        bool enabledOnly = false,
        string? sorting = null,
        int skipCount = 0,
        int maxResultCount = int.MaxValue,
        CancellationToken cancellationToken = default)
    {
        var queryable = (await GetQueryableAsync()).Include(x => x.Stages).AsQueryable();
        if (enabledOnly)
        {
            queryable = queryable.Where(x => x.IsEnabled);
        }

        var totalCount = await AsyncExecuter.CountAsync(queryable, GetCancellationToken(cancellationToken));

        var items = await AsyncExecuter.ToListAsync(
            queryable
                .OrderBy(sorting.IsNullOrWhiteSpace() ? "CreationTime desc" : sorting)
                .Skip(skipCount)
                .Take(maxResultCount),
            GetCancellationToken(cancellationToken));

        return (items, totalCount);
    }

    public async Task<List<SmartOffer>> GetEnabledForTenantsAsync(
        IReadOnlyCollection<Guid> tenantIds,
        CancellationToken cancellationToken = default)
    {
        var ids = tenantIds.Select(id => (Guid?)id).ToList();
        var query = (await GetQueryableAsync())
            .Include(x => x.Stages)
            .Where(x => x.IsEnabled && ids.Contains(x.TenantId));

        return await AsyncExecuter.ToListAsync(query, GetCancellationToken(cancellationToken));
    }

    public async Task<SmartOffer?> FindWithStagesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await (await GetQueryableAsync())
            .Include(x => x.Stages)
            .FirstOrDefaultAsync(x => x.Id == id, GetCancellationToken(cancellationToken));
    }
}
