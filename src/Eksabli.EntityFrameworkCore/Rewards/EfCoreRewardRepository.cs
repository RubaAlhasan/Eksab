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

namespace Eksabli.Rewards;

public class EfCoreRewardRepository : EfCoreRepository<EksabliDbContext, Reward, Guid>, IRewardRepository
{
    private readonly IClock _clock;

    public EfCoreRewardRepository(IDbContextProvider<EksabliDbContext> dbContextProvider, IClock clock)
        : base(dbContextProvider)
    {
        _clock = clock;
    }

    public async Task<(List<Reward> Items, int TotalCount)> GetListAsync(
        string? filterText = null,
        bool activeOnly = false,
        string? sorting = null,
        int skipCount = 0,
        int maxResultCount = int.MaxValue,
        CancellationToken cancellationToken = default)
    {
        var queryable = ApplyFilter(await GetQueryableAsync(), filterText, activeOnly);

        var totalCount = await AsyncExecuter.CountAsync(queryable, GetCancellationToken(cancellationToken));

        var items = await AsyncExecuter.ToListAsync(
            queryable
                .OrderBy(sorting.IsNullOrWhiteSpace() ? "NameEn" : sorting)
                .Skip(skipCount)
                .Take(maxResultCount),
            GetCancellationToken(cancellationToken));

        return (items, totalCount);
    }

    protected virtual IQueryable<Reward> ApplyFilter(IQueryable<Reward> query, string? filterText, bool activeOnly)
    {
        if (!filterText.IsNullOrWhiteSpace())
        {
            query = query.Where(x => x.NameEn.Contains(filterText!) || x.NameAr.Contains(filterText!));
        }

        if (activeOnly)
        {
            var now = _clock.Now;
            query = query.Where(x => x.StockRemaining == null || x.StockRemaining > 0);
            query = query.Where(x => x.ValidFrom == null || x.ValidFrom <= now);
            query = query.Where(x => x.ValidTo == null || x.ValidTo >= now);
        }

        return query;
    }
}
