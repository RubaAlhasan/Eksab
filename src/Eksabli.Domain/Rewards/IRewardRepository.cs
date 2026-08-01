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
}
