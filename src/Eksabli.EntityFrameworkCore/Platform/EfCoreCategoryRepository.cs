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

namespace Eksabli.Platform;

public class EfCoreCategoryRepository : EfCoreRepository<EksabliDbContext, Category, Guid>, ICategoryRepository
{
    public EfCoreCategoryRepository(IDbContextProvider<EksabliDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public async Task<(List<Category> Items, int TotalCount)> GetListAsync(
        Guid? parentCategoryId = null,
        string? filterText = null,
        string? sorting = null,
        int skipCount = 0,
        int maxResultCount = int.MaxValue,
        CancellationToken cancellationToken = default)
    {
        var queryable = ApplyFilter(await GetQueryableAsync(), parentCategoryId, filterText);

        var totalCount = await AsyncExecuter.CountAsync(queryable, GetCancellationToken(cancellationToken));

        var items = await AsyncExecuter.ToListAsync(
            queryable
                .OrderBy(sorting.IsNullOrWhiteSpace() ? "NameEn" : sorting)
                .Skip(skipCount)
                .Take(maxResultCount),
            GetCancellationToken(cancellationToken));

        return (items, totalCount);
    }

    protected virtual IQueryable<Category> ApplyFilter(IQueryable<Category> query, Guid? parentCategoryId, string? filterText)
    {
        if (parentCategoryId.HasValue)
        {
            query = query.Where(x => x.ParentCategoryId == parentCategoryId.Value);
        }

        if (!filterText.IsNullOrWhiteSpace())
        {
            query = query.Where(x => x.NameEn.Contains(filterText!) || x.NameAr.Contains(filterText!));
        }

        return query;
    }
}
