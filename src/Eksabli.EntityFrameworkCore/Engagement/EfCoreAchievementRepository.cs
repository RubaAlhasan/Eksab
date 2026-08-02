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

namespace Eksabli.Engagement;

public class EfCoreAchievementRepository : EfCoreRepository<EksabliDbContext, Achievement, Guid>, IAchievementRepository
{
    public EfCoreAchievementRepository(IDbContextProvider<EksabliDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public async Task<(List<Achievement> Items, int TotalCount)> GetListAsync(
        Guid? tenantId,
        string? sorting = null,
        int skipCount = 0,
        int maxResultCount = int.MaxValue,
        CancellationToken cancellationToken = default)
    {
        var queryable = (await GetQueryableAsync()).Where(a => a.TenantId == null || a.TenantId == tenantId);

        var totalCount = await AsyncExecuter.CountAsync(queryable, GetCancellationToken(cancellationToken));

        var items = await AsyncExecuter.ToListAsync(
            queryable
                .OrderBy(sorting.IsNullOrWhiteSpace() ? "Name" : sorting)
                .Skip(skipCount)
                .Take(maxResultCount),
            GetCancellationToken(cancellationToken));

        return (items, totalCount);
    }
}
