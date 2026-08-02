using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace Eksabli.Offers;

public interface IOfferRepository : IRepository<Offer, Guid>
{
    Task<(List<Offer> Items, int TotalCount)> GetListAsync(
        Guid? branchId = null,
        bool activeOnly = false,
        string? sorting = null,
        int skipCount = 0,
        int maxResultCount = int.MaxValue,
        CancellationToken cancellationToken = default);
}
