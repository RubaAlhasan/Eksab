using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Users;

namespace Eksabli.Notifications;

[Authorize]
[RemoteService(IsEnabled = false)]
public class CustomerNotificationPreferenceAppService : ApplicationService, ICustomerNotificationPreferenceAppService
{
    private readonly IRepository<NotificationGroupOptOut, Guid> _optOutRepository;

    public CustomerNotificationPreferenceAppService(IRepository<NotificationGroupOptOut, Guid> optOutRepository)
    {
        _optOutRepository = optOutRepository;
    }

    public async Task<NotificationPreferencesDto> GetMyAsync()
    {
        var userId = CurrentUser.GetId();
        var optedOut = (await _optOutRepository.GetListAsync(o => o.UserId == userId)).Select(o => o.Group).ToHashSet();
        return ToDto(optedOut);
    }

    public async Task<NotificationPreferencesDto> UpdateMyAsync(NotificationPreferencesDto input)
    {
        var userId = CurrentUser.GetId();
        var existing = await _optOutRepository.GetListAsync(o => o.UserId == userId);

        var wanted = new Dictionary<NotificationGroup, bool>
        {
            [NotificationGroup.Rewards] = input.Rewards,
            [NotificationGroup.Deals] = input.Deals,
            [NotificationGroup.Offers] = input.Offers,
        };

        foreach (var (group, enabled) in wanted)
        {
            var row = existing.FirstOrDefault(o => o.Group == group);
            if (enabled && row != null)
            {
                await _optOutRepository.DeleteAsync(row);
            }
            else if (!enabled && row == null)
            {
                await _optOutRepository.InsertAsync(NotificationGroupOptOut.Create(GuidGenerator.Create(), userId, group));
            }
        }

        return ToDto(wanted.Where(kv => !kv.Value).Select(kv => kv.Key).ToHashSet());
    }

    private static NotificationPreferencesDto ToDto(HashSet<NotificationGroup> optedOut) => new()
    {
        Rewards = !optedOut.Contains(NotificationGroup.Rewards),
        Deals = !optedOut.Contains(NotificationGroup.Deals),
        Offers = !optedOut.Contains(NotificationGroup.Offers),
    };
}
