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
using Volo.Abp.Timing;

namespace Eksabli.Offers;

public class EfCoreOfferRepository : EfCoreRepository<EksabliDbContext, Offer, Guid>, IOfferRepository
{
    private readonly IClock _clock;

    public EfCoreOfferRepository(IDbContextProvider<EksabliDbContext> dbContextProvider, IClock clock)
        : base(dbContextProvider)
    {
        _clock = clock;
    }

    public async Task<(List<Offer> Items, int TotalCount)> GetListAsync(
        Guid? branchId = null,
        bool activeOnly = false,
        string? sorting = null,
        int skipCount = 0,
        int maxResultCount = int.MaxValue,
        CancellationToken cancellationToken = default)
    {
        var queryable = ApplyFilter(await GetQueryableAsync(), branchId, activeOnly);

        var totalCount = await AsyncExecuter.CountAsync(queryable, GetCancellationToken(cancellationToken));

        var items = await AsyncExecuter.ToListAsync(
            queryable
                .OrderBy(sorting.IsNullOrWhiteSpace() ? "StartDate desc" : sorting)
                .Skip(skipCount)
                .Take(maxResultCount),
            GetCancellationToken(cancellationToken));

        return (items, totalCount);
    }

    protected virtual IQueryable<Offer> ApplyFilter(IQueryable<Offer> query, Guid? branchId, bool activeOnly)
    {
        if (branchId.HasValue)
        {
            query = query.Where(x => x.BranchId == branchId.Value);
        }

        if (activeOnly)
        {
            var now = _clock.Now;
            query = query.Where(x => x.StartDate <= now && x.EndDate >= now);
        }

        return query;
    }
}
