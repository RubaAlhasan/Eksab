using System.Collections.Generic;
using System.Threading.Tasks;
using Eksabli.Wallets;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Eksabli.Memberships;

// Exposed via an explicit controller (src/Eksabli.HttpApi/Controllers/MembershipsController.cs).
[RemoteService(IsEnabled = false)]
public interface IMembershipAppService : IApplicationService
{
    Task<MembershipDto> JoinAsync(JoinBusinessDto input);

    Task<List<MembershipDto>> GetMyMembershipsAsync();

    Task<List<PointsWalletDto>> GetMyWalletsAsync();

    Task<WalletQrTokenResultDto> GetMyWalletQrTokenAsync();

    // Business Portal > Customers "Members" tab (Eksabli.Memberships.View) — ambient tenant, this
    // business's own members only, same realm-scoping shape as FollowAppService.GetFollowersAsync.
    Task<PagedResultDto<MemberDto>> GetMembersAsync(MemberFilterDto input);
}
