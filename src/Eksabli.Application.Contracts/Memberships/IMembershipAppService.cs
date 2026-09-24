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

    // Customer-initiated leave — see MembershipStatus.Cancelled's own comment. Idempotent-feeling to
    // the caller (throws a UserFriendlyException, not 404/500, if they're not currently an active
    // member of this tenant), same "expected, user-facing failure" shape as JoinAsync's own guards.
    Task LeaveAsync(System.Guid tenantId);

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

    // Staff-initiated suspension (Eksabli.Memberships.Edit) — unlike LeaveAsync (customer-initiated,
    // keyed by tenantId since a customer only ever has one membership per tenant), these are keyed by
    // membership id since staff act on a specific row from the Members list. Only a currently-Active
    // membership can be frozen; JoinAsync deliberately blocks a customer from undoing this themselves
    // by rejoining — see its own comment.
    Task FreezeAsync(System.Guid id);

    // Only a currently-Frozen membership can be reactivated — a Cancelled (customer-left) one is
    // reactivated by the customer themselves rejoining (JoinAsync), not by staff here.
    Task ReactivateAsync(System.Guid id);
}
