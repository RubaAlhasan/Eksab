using System;
using System.Threading.Tasks;
using Eksabli.Permissions;
using Eksabli.Platform;
using Eksabli.Wallets;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;

namespace Eksabli.Controllers;

[ApiController]
[Route("api/app/admin-users")]
[Authorize(EksabliPermissions.Users.View)]
public class AdminUsersController : EksabliController
{
    private readonly IAdminUserAppService _adminUserAppService;

    public AdminUsersController(IAdminUserAppService adminUserAppService)
    {
        _adminUserAppService = adminUserAppService;
    }

    [HttpGet]
    public Task<PagedResultDto<AdminUserDto>> GetListAsync([FromQuery] AdminUserFilterDto input)
    {
        return _adminUserAppService.GetListAsync(input);
    }

    [HttpGet("{customerId:guid}")]
    public Task<AdminCustomerDetailDto> GetCustomerDetailAsync(Guid customerId)
    {
        return _adminUserAppService.GetCustomerDetailAsync(customerId);
    }

    [HttpGet("memberships/{membershipId:guid}/transactions")]
    public Task<PagedResultDto<PointsTransactionDto>> GetCustomerTransactionsAsync(
        Guid membershipId, [FromQuery] Guid tenantId, [FromQuery] PagedAndSortedResultRequestDto input)
    {
        return _adminUserAppService.GetCustomerTransactionsAsync(membershipId, tenantId, input);
    }
}
