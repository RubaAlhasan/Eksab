using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace Eksabli.Rewards;

public interface IRewardRepository : IRepository<Reward, Guid>
{
    Task<(List<Reward> Items, int TotalCount)> GetListAsync(
        string? filterText = null,
        bool activeOnly = false,
        string? sorting = null,
        int skipCount = 0,
        int maxResultCount = int.MaxValue,
        CancellationToken cancellationToken = default);

    // Same "active" definition as the activeOnly branch above (in stock, within its validity window),
    // but across many tenants in one query instead of the ambient ICurrentTenant — for a cross-business
    // feed, the same shape ISmartOfferRepository.GetEnabledForTenantsAsync already uses.
    Task<List<Reward>> GetActiveForTenantsAsync(IReadOnlyCollection<Guid> tenantIds, CancellationToken cancellationToken = default);
}
