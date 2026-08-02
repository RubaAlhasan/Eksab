using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace Eksabli.Platform;

public interface ISupportTicketRepository : IRepository<SupportTicket, Guid>
{
    Task<(List<SupportTicket> Items, int TotalCount)> GetListAsync(
        SupportTicketStatus? status = null,
        SupportTicketPriority? priority = null,
        Guid? tenantId = null,
        Guid? customerId = null,
        string? sorting = null,
        int skipCount = 0,
        int maxResultCount = int.MaxValue,
        CancellationToken cancellationToken = default);
}
