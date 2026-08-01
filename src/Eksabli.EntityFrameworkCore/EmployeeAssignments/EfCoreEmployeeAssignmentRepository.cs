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

namespace Eksabli.EmployeeAssignments;

public class EfCoreEmployeeAssignmentRepository : EfCoreRepository<EksabliDbContext, EmployeeAssignment, Guid>, IEmployeeAssignmentRepository
{
    public EfCoreEmployeeAssignmentRepository(IDbContextProvider<EksabliDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public async Task<(List<EmployeeAssignment> Items, int TotalCount)> GetListAsync(
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
                .OrderBy(sorting.IsNullOrWhiteSpace() ? "CreationTime desc" : sorting)
                .Skip(skipCount)
                .Take(maxResultCount),
            GetCancellationToken(cancellationToken));

        return (items, totalCount);
    }

    // EmployeeAssignment has no natural text field of its own (UserId/Role aren't free text) —
    // filterText is accepted for interface consistency but currently a no-op.
    protected virtual IQueryable<EmployeeAssignment> ApplyFilter(IQueryable<EmployeeAssignment> query, string? filterText)
    {
        return query;
    }
}
