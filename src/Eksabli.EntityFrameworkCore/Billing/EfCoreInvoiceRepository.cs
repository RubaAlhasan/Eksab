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

public class EfCoreInvoiceRepository : EfCoreRepository<EksabliDbContext, Invoice, Guid>, IInvoiceRepository
{
    public EfCoreInvoiceRepository(IDbContextProvider<EksabliDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public async Task<(List<Invoice> Items, int TotalCount)> GetListAsync(
        Guid? tenantSubscriptionId = null,
        InvoiceStatus? status = null,
        string? sorting = null,
        int skipCount = 0,
        int maxResultCount = int.MaxValue,
        CancellationToken cancellationToken = default)
    {
        var queryable = ApplyFilter(await GetQueryableAsync(), tenantSubscriptionId, status);

        var totalCount = await AsyncExecuter.CountAsync(queryable, GetCancellationToken(cancellationToken));

        var items = await AsyncExecuter.ToListAsync(
            queryable
                .OrderBy(sorting.IsNullOrWhiteSpace() ? "DueDate desc" : sorting)
                .Skip(skipCount)
                .Take(maxResultCount),
            GetCancellationToken(cancellationToken));

        return (items, totalCount);
    }

    protected virtual IQueryable<Invoice> ApplyFilter(IQueryable<Invoice> query, Guid? tenantSubscriptionId, InvoiceStatus? status)
    {
        if (tenantSubscriptionId.HasValue)
        {
            query = query.Where(x => x.TenantSubscriptionId == tenantSubscriptionId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        return query;
    }
}
