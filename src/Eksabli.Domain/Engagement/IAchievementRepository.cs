using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace Eksabli.Engagement;

public interface IAchievementRepository : IRepository<Achievement, Guid>
{
    // Platform-wide (TenantId == null) + this tenant's own achievements — see Achievement's own
    // comment for why this can't just be the standard IMultiTenant filter.
    Task<(List<Achievement> Items, int TotalCount)> GetListAsync(
        Guid? tenantId,
        string? sorting = null,
        int skipCount = 0,
        int maxResultCount = int.MaxValue,
        CancellationToken cancellationToken = default);
}
