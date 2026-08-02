using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace Eksabli.Billing;

public interface IInvoiceRepository : IRepository<Invoice, Guid>
{
    Task<(List<Invoice> Items, int TotalCount)> GetListAsync(
        Guid? tenantSubscriptionId = null,
        InvoiceStatus? status = null,
        string? sorting = null,
        int skipCount = 0,
        int maxResultCount = int.MaxValue,
        CancellationToken cancellationToken = default);
}
