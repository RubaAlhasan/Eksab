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

namespace Eksabli.Wallets;

public class EfCorePointsTransactionRepository : EfCoreRepository<EksabliDbContext, PointsTransaction, Guid>, IPointsTransactionRepository
{
    public EfCorePointsTransactionRepository(IDbContextProvider<EksabliDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public async Task<(List<PointsTransaction> Items, int TotalCount)> GetListAsync(
        PointsTransactionType? type = null,
        Guid? createdByEmployeeId = null,
        ICollection<Guid>? createdByEmployeeIds = null,
        Guid? walletId = null,
        DateTime? from = null,
        DateTime? to = null,
        string? sorting = null,
        int skipCount = 0,
        int maxResultCount = int.MaxValue,
        CancellationToken cancellationToken = default)
    {
        var queryable = ApplyFilter(await GetQueryableAsync(), type, createdByEmployeeId, createdByEmployeeIds, walletId, from, to);

        var totalCount = await AsyncExecuter.CountAsync(queryable, GetCancellationToken(cancellationToken));

        var items = await AsyncExecuter.ToListAsync(
            queryable
                .OrderBy(sorting.IsNullOrWhiteSpace() ? "CreationTime desc" : sorting)
                .Skip(skipCount)
                .Take(maxResultCount),
            GetCancellationToken(cancellationToken));

        return (items, totalCount);
    }

    protected virtual IQueryable<PointsTransaction> ApplyFilter(
        IQueryable<PointsTransaction> query,
        PointsTransactionType? type,
        Guid? createdByEmployeeId,
        ICollection<Guid>? createdByEmployeeIds,
        Guid? walletId,
        DateTime? from,
        DateTime? to)
    {
        if (type.HasValue)
        {
            query = query.Where(t => t.Type == type.Value);
        }

        if (createdByEmployeeId.HasValue)
        {
            query = query.Where(t => t.CreatedByEmployeeId == createdByEmployeeId.Value);
        }

        if (createdByEmployeeIds != null)
        {
            query = query.Where(t => t.CreatedByEmployeeId.HasValue && createdByEmployeeIds.Contains(t.CreatedByEmployeeId.Value));
        }

        if (walletId.HasValue)
        {
            query = query.Where(t => t.WalletId == walletId.Value);
        }

        if (from.HasValue)
        {
            query = query.Where(t => t.CreationTime >= from.Value);
        }

        if (to.HasValue)
        {
            query = query.Where(t => t.CreationTime <= to.Value);
        }

        return query;
    }
}
