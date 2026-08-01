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

namespace Eksabli.Rewards;

public class EfCoreCouponRepository : EfCoreRepository<EksabliDbContext, Coupon, Guid>, ICouponRepository
{
    public EfCoreCouponRepository(IDbContextProvider<EksabliDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public async Task<(List<Coupon> Items, int TotalCount)> GetListAsync(
        CouponStatus? status = null,
        Guid? branchId = null,
        string? sorting = null,
        int skipCount = 0,
        int maxResultCount = int.MaxValue,
        CancellationToken cancellationToken = default)
    {
        var queryable = ApplyFilter(await GetQueryableAsync(), status, branchId);

        var totalCount = await AsyncExecuter.CountAsync(queryable, GetCancellationToken(cancellationToken));

        var items = await AsyncExecuter.ToListAsync(
            queryable
                .OrderBy(sorting.IsNullOrWhiteSpace() ? "CreationTime desc" : sorting)
                .Skip(skipCount)
                .Take(maxResultCount),
            GetCancellationToken(cancellationToken));

        return (items, totalCount);
    }

    protected virtual IQueryable<Coupon> ApplyFilter(IQueryable<Coupon> query, CouponStatus? status, Guid? branchId)
    {
        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        if (branchId.HasValue)
        {
            query = query.Where(x => x.RedeemedBranchId == branchId.Value);
        }

        return query;
    }
}
