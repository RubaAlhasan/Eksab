using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace Eksabli.SmartOffers;

public interface ISmartOfferRepository : IRepository<SmartOffer, Guid>
{
    // Loads offers together with their stages, so the price for an offer is never resolved against a partial
    // stage list.
    Task<(List<SmartOffer> Items, int TotalCount)> GetListAsync(
        bool enabledOnly = false,
        string? sorting = null,
        int skipCount = 0,
        int maxResultCount = int.MaxValue,
        CancellationToken cancellationToken = default);

    Task<SmartOffer?> FindWithStagesAsync(Guid id, CancellationToken cancellationToken = default);

    // Enabled offers from the given businesses, with their stages. The caller decides which of them are live.
    Task<List<SmartOffer>> GetEnabledForTenantsAsync(IReadOnlyCollection<Guid> tenantIds, CancellationToken cancellationToken = default);
}
