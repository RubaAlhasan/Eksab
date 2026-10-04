using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Eksabli.Memberships;
using Eksabli.Permissions;
using Eksabli.Reports;
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
    private readonly IReportsAppService _reportsAppService;

    public MembershipsController(IMembershipAppService membershipAppService, IReportsAppService reportsAppService)
    {
        _membershipAppService = membershipAppService;
        _reportsAppService = reportsAppService;
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

    // The customer page's "Smart deal sales" tab. Gated by Memberships.View, the permission the page itself needs, not by
    // Reports.Default: a manager who can open the customer can see that customer's sales, and nothing else. The membership id
    // comes from the route, so the filter always names one of this business's own customers.
    [Authorize(EksabliPermissions.Memberships.View)]
    [HttpGet("{id}/smart-deal-sales")]
    public Task<PagedResultDto<SmartDealSaleDto>> GetSmartDealSalesAsync(Guid id, [FromQuery] SmartDealSaleFilterDto input)
    {
        input.MembershipId = id;
        return _reportsAppService.GetSmartDealSalesAsync(input);
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
