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

namespace Eksabli.Notifications;

public class EfCoreNotificationRepository : EfCoreRepository<EksabliDbContext, Notification, Guid>, INotificationRepository
{
    public EfCoreNotificationRepository(IDbContextProvider<EksabliDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public async Task<(List<Notification> Items, int TotalCount)> GetListAsync(
        Guid? campaignId = null,
        NotificationStatus? status = null,
        NotificationChannel? channel = null,
        string? sorting = null,
        int skipCount = 0,
        int maxResultCount = int.MaxValue,
        CancellationToken cancellationToken = default)
    {
        var queryable = ApplyFilter(await GetQueryableAsync(), campaignId, status, channel);

        var totalCount = await AsyncExecuter.CountAsync(queryable, GetCancellationToken(cancellationToken));

        var items = await AsyncExecuter.ToListAsync(
            queryable
                .OrderBy(sorting.IsNullOrWhiteSpace() ? "CreationTime desc" : sorting)
                .Skip(skipCount)
                .Take(maxResultCount),
            GetCancellationToken(cancellationToken));

        return (items, totalCount);
    }

    public async Task<bool> ExistsForCampaignAsync(Guid campaignId, Guid membershipId, CancellationToken cancellationToken = default)
    {
        var queryable = await GetQueryableAsync();
        return await AsyncExecuter.AnyAsync(
            queryable.Where(x => x.CampaignId == campaignId && x.MembershipId == membershipId),
            GetCancellationToken(cancellationToken));
    }

    public async Task<int> CountCreatedSinceAsync(DateTime sinceUtc, CancellationToken cancellationToken = default)
    {
        var queryable = await GetQueryableAsync();
        return await AsyncExecuter.CountAsync(
            queryable.Where(x => x.CreationTime >= sinceUtc),
            GetCancellationToken(cancellationToken));
    }

    protected virtual IQueryable<Notification> ApplyFilter(
        IQueryable<Notification> query, Guid? campaignId, NotificationStatus? status, NotificationChannel? channel)
    {
        if (campaignId.HasValue)
        {
            query = query.Where(x => x.CampaignId == campaignId.Value);
        }

        if (status.HasValue)
        {
            query = query.Where(x => x.Status == status.Value);
        }

        if (channel.HasValue)
        {
            query = query.Where(x => x.Channel == channel.Value);
        }

        return query;
    }
}
