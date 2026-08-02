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

public class EfCoreSubscriptionPlanRepository : EfCoreRepository<EksabliDbContext, SubscriptionPlan, Guid>, ISubscriptionPlanRepository
{
    public EfCoreSubscriptionPlanRepository(IDbContextProvider<EksabliDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public async Task<(List<SubscriptionPlan> Items, int TotalCount)> GetListAsync(
        string? filterText = null,
        string? sorting = null,
        int skipCount = 0,
        int maxResultCount = int.MaxValue,
        CancellationToken cancellationToken = default)
    {
        var queryable = ApplyFilter(await GetQueryableAsync(), filterText);

        var totalCount = await AsyncExecuter.CountAsync(queryable, GetCancellationToken(cancellationToken));

        var items = await AsyncExecuter.ToListAsync(
            queryable
                .OrderBy(sorting.IsNullOrWhiteSpace() ? "MonthlyPrice" : sorting)
                .Skip(skipCount)
                .Take(maxResultCount),
            GetCancellationToken(cancellationToken));

        return (items, totalCount);
    }

    protected virtual IQueryable<SubscriptionPlan> ApplyFilter(IQueryable<SubscriptionPlan> query, string? filterText)
    {
        if (!filterText.IsNullOrWhiteSpace())
        {
            query = query.Where(x => x.Name.Contains(filterText!));
        }

        return query;
    }
}
