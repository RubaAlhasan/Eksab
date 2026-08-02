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

namespace Eksabli.Platform;

public class EfCoreSupportTicketRepository : EfCoreRepository<EksabliDbContext, SupportTicket, Guid>, ISupportTicketRepository
{
    public EfCoreSupportTicketRepository(IDbContextProvider<EksabliDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public override async Task<IQueryable<SupportTicket>> WithDetailsAsync()
    {
        return (await GetQueryableAsync()).Include(x => x.Messages);
    }

    public async Task<(List<SupportTicket> Items, int TotalCount)> GetListAsync(
        SupportTicketStatus? status = null,
        SupportTicketPriority? priority = null,
        Guid? tenantId = null,
        Guid? customerId = null,
        string? sorting = null,
        int skipCount = 0,
        int maxResultCount = int.MaxValue,
        CancellationToken cancellationToken = default)
    {
        // Intentionally the plain (non-details) queryable — the queue view doesn't need the message
        // thread; use GetAsync for the full aggregate (ticket detail screen).
        var queryable = ApplyFilter(await GetQueryableAsync(), status, priority, tenantId, customerId);

        var totalCount = await AsyncExecuter.CountAsync(queryable, GetCancellationToken(cancellationToken));

        var items = await AsyncExecuter.ToListAsync(
            queryable
                .OrderBy(sorting.IsNullOrWhiteSpace() ? "CreationTime desc" : sorting)
                .Skip(skipCount)
                .Take(maxResultCount),
            GetCancellationToken(cancellationToken));

        return (items, totalCount);
    }

    protected virtual IQueryable<SupportTicket> ApplyFilter(
        IQueryable<SupportTicket> query, SupportTicketStatus? status, SupportTicketPriority? priority, Guid? tenantId, Guid? customerId)
    {
        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        if (priority.HasValue)
        {
            query = query.Where(x => x.Priority == priority.Value);
        }

        if (tenantId.HasValue)
        {
            query = query.Where(x => x.TenantId == tenantId.Value);
        }

        if (customerId.HasValue)
        {
            query = query.Where(x => x.CustomerId == customerId.Value);
        }

        return query;
    }
}
