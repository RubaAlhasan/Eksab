using System.Collections.Generic;
using System.Threading.Tasks;
using Eksabli.Memberships;
using Eksabli.Wallets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eksabli.Controllers;

[ApiController]
[Route("api/app/memberships")]
[Authorize]
public class MembershipsController : EksabliController
{
    private readonly IMembershipAppService _membershipAppService;

    public MembershipsController(IMembershipAppService membershipAppService)
    {
        _membershipAppService = membershipAppService;
    }

    [HttpPost("join")]
    public Task<MembershipDto> JoinAsync(JoinBusinessDto input)
    {
        return _membershipAppService.JoinAsync(input);
    }

    [HttpGet("my")]
    public Task<List<MembershipDto>> GetMyMembershipsAsync()
    {
        return _membershipAppService.GetMyMembershipsAsync();
    }

    [HttpGet("my/wallets")]
    public Task<List<PointsWalletDto>> GetMyWalletsAsync()
    {
        return _membershipAppService.GetMyWalletsAsync();
    }

    [HttpPost("my/wallet-qr-token")]
    public Task<WalletQrTokenResultDto> GetMyWalletQrTokenAsync()
    {
        return _membershipAppService.GetMyWalletQrTokenAsync();
    }
}
