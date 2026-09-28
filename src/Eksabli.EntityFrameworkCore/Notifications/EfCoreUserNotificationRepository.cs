using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Eksabli.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;

namespace Eksabli.Notifications;

// UserNotification.TenantId is the SENDING business's tenant (see that class's own comment), but the
// customer reading their feed is a Host-realm identity who can belong to many different tenants at
// once — the feed has to read across every one of them, scoped by UserId alone, the same "one customer,
// many tenants" shape AdminUserAppService.GetCustomerDetailAsync already had to disable the filter for
// when reading a customer's memberships. UserNotification.cs's own comment claims "ABP's standard
// IMultiTenant data filter 'just works' for feed queries" — that's only true for a Host-realm-addressed
// row (TenantId=null); every business-originated notification (campaign bonus, referral, staff
// Compose — the actual point of this feed) has a real business TenantId, and the reading customer's own
// ambient tenant is never reliably that one specific business, so the standard filter silently excluded
// them. Confirmed live: GetUnreadCountAsync and GetFeedAsync disagreeing (a nonzero badge count against
// an empty feed) for the exact same rows.
public class EfCoreUserNotificationRepository : EfCoreRepository<EksabliDbContext, UserNotification, Guid>, IUserNotificationRepository
{
    private readonly IDataFilter _dataFilter;

    public EfCoreUserNotificationRepository(IDbContextProvider<EksabliDbContext> dbContextProvider, IDataFilter dataFilter)
        : base(dbContextProvider)
    {
        _dataFilter = dataFilter;
    }

    public async Task<(List<UserNotificationFeedItem> Items, long TotalCount)> GetFeedAsync(
        Guid userId,
        bool? isRead = null,
        int skipCount = 0,
        int maxResultCount = 20,
        CancellationToken cancellationToken = default)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var dbContext = await GetDbContextAsync();

            // Recipient/content join — see NotificationMessage's class comment for why the two are split.
            var query =
                from recipient in dbContext.Set<UserNotification>()
                join message in dbContext.Set<NotificationMessage>() on recipient.NotificationMessageId equals message.Id
                where recipient.UserId == userId
                select new { recipient, message };

            if (isRead.HasValue)
            {
                query = query.Where(x => x.recipient.IsRead == isRead.Value);
            }

            var totalCount = await AsyncExecuter.LongCountAsync(query, GetCancellationToken(cancellationToken));

            var page = await AsyncExecuter.ToListAsync(
                query
                    .OrderByDescending(x => x.recipient.CreationTime)
                    .Skip(skipCount)
                    .Take(maxResultCount),
                GetCancellationToken(cancellationToken));

            var items = page.Select(x => new UserNotificationFeedItem
            {
                Id = x.recipient.Id,
                Type = x.message.Type,
                Title = x.message.Title,
                Message = x.message.Message,
                Category = x.message.Category,
                Data = x.message.Data,
                IsRead = x.recipient.IsRead,
                ReadAt = x.recipient.ReadAt,
                CreationTime = x.recipient.CreationTime
            }).ToList();

            return (items, totalCount);
        }
    }

    public async Task<long> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var queryable = await GetQueryableAsync();
            return await AsyncExecuter.LongCountAsync(
                queryable.Where(x => x.UserId == userId && !x.IsRead),
                GetCancellationToken(cancellationToken));
        }
    }

    public async Task MarkAllAsReadAsync(Guid userId, DateTime readAt, CancellationToken cancellationToken = default)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var queryable = await GetQueryableAsync();
            var unread = await AsyncExecuter.ToListAsync(
                queryable.Where(x => x.UserId == userId && !x.IsRead),
                GetCancellationToken(cancellationToken));

            if (unread.Count == 0)
            {
                return;
            }

            foreach (var row in unread)
            {
                row.MarkAsRead(readAt);
            }

            await UpdateManyAsync(unread, cancellationToken: cancellationToken);
        }
    }
}
