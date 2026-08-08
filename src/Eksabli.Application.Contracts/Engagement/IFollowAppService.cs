using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Eksabli.Engagement;

// Exposed via an explicit controller (src/Eksabli.HttpApi/Controllers/FollowsController.cs).
[RemoteService(IsEnabled = false)]
public interface IFollowAppService : IApplicationService
{
    // Host-realm, customer-scoped.
    Task FollowAsync(Guid tenantId);

    Task UnfollowAsync(Guid tenantId);

    // "Favorites" — followed-but-not-necessarily-joined businesses, across every tenant.
    Task<List<FollowDto>> GetMyFollowsAsync();

    // Business-side "Followers list" (Eksabli.Followers.View) — ambient tenant.
    Task<PagedResultDto<FollowerDto>> GetFollowersAsync(PagedAndSortedResultRequestDto input);
}
