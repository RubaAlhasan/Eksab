using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace Eksabli.Campaigns;

public interface ICampaignRepository : IRepository<Campaign, Guid>
{
    Task<(List<Campaign> Items, int TotalCount)> GetListAsync(
        CampaignStatus? status = null,
        CampaignType? type = null,
        string? sorting = null,
        int skipCount = 0,
        int maxResultCount = int.MaxValue,
        CancellationToken cancellationToken = default);
}
