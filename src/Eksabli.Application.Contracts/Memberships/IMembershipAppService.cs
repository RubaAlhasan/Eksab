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

    // Business Portal > Customer Details (Eksabli.Memberships.View, same permission as the list) — the
    // single-row counterpart GetMembersAsync never had; a details page needs a stable, direct-URL-
    // loadable fetch, not a client-side scan of the full member list.
    Task<MemberDto> GetMemberAsync(System.Guid id);
}
