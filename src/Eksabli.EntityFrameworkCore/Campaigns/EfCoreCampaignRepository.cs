using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading;
using System.Threading.Tasks;
using Eksabli.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Volo.Abp;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace Eksabli.Campaigns;

public class EfCoreCampaignRepository : EfCoreRepository<EksabliDbContext, Campaign, Guid>, ICampaignRepository
{
    public EfCoreCampaignRepository(IDbContextProvider<EksabliDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public override async Task<IQueryable<Campaign>> WithDetailsAsync()
    {
        return (await GetQueryableAsync()).Include(x => x.TargetRules);
    }

    public async Task<(List<Campaign> Items, int TotalCount)> GetListAsync(
        CampaignStatus? status = null,
        CampaignType? type = null,
        string? sorting = null,
        int skipCount = 0,
        int maxResultCount = int.MaxValue,
        CancellationToken cancellationToken = default)
    {
        // Intentionally the plain (non-details) queryable — the list view doesn't need TargetRules;
        // use GetAsync for the full aggregate (edit screen / segment evaluation).
        var queryable = ApplyFilter(await GetQueryableAsync(), status, type);

        var totalCount = await AsyncExecuter.CountAsync(queryable, GetCancellationToken(cancellationToken));

        var items = await AsyncExecuter.ToListAsync(
            queryable
                .OrderBy(sorting.IsNullOrWhiteSpace() ? "CreationTime desc" : sorting)
                .Skip(skipCount)
                .Take(maxResultCount),
            GetCancellationToken(cancellationToken));

        return (items, totalCount);
    }

    protected virtual IQueryable<Campaign> ApplyFilter(IQueryable<Campaign> query, CampaignStatus? status, CampaignType? type)
    {
        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        if (type.HasValue)
        {
            query = query.Where(x => x.Type == type.Value);
        }

        return query;
    }
}
