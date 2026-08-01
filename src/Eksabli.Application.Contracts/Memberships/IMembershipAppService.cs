using System.Collections.Generic;
using System.Threading.Tasks;
using Eksabli.Wallets;
using Volo.Abp;
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
}
