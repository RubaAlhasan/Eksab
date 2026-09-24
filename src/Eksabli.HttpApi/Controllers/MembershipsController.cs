using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Eksabli.Memberships;
using Eksabli.Permissions;
using Eksabli.Wallets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;

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

    [Authorize(EksabliPermissions.Memberships.View)]
    [HttpGet]
    public Task<PagedResultDto<MemberDto>> GetMembersAsync([FromQuery] MemberFilterDto input)
    {
        return _membershipAppService.GetMembersAsync(input);
    }

    [Authorize(EksabliPermissions.Memberships.View)]
    [HttpGet("{id}")]
    public Task<MemberDto> GetMemberAsync(Guid id)
    {
        return _membershipAppService.GetMemberAsync(id);
    }

    [Authorize(EksabliPermissions.Memberships.Edit)]
    [HttpPost("{id}/freeze")]
    public Task FreezeAsync(Guid id)
    {
        return _membershipAppService.FreezeAsync(id);
    }

    [Authorize(EksabliPermissions.Memberships.Edit)]
    [HttpPost("{id}/reactivate")]
    public Task ReactivateAsync(Guid id)
    {
        return _membershipAppService.ReactivateAsync(id);
    }

    [HttpPost("join")]
    public Task<MembershipDto> JoinAsync(JoinBusinessDto input)
    {
        return _membershipAppService.JoinAsync(input);
    }

    [HttpPost("{tenantId}/leave")]
    public Task LeaveAsync(Guid tenantId)
    {
        return _membershipAppService.LeaveAsync(tenantId);
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
