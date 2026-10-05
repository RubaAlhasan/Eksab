using System;
using System.Threading.Tasks;
using Eksabli.Permissions;
using Eksabli.Platform;
using Eksabli.Reports;
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

    // The admin customer page's per-business "Smart deal sales" view. Same Users.View gate as the transactions beside it.
    [HttpGet("memberships/{membershipId:guid}/smart-deal-sales")]
    public Task<PagedResultDto<SmartDealSaleDto>> GetCustomerSmartDealSalesAsync(
        Guid membershipId, [FromQuery] Guid tenantId, [FromQuery] PagedAndSortedResultRequestDto input)
    {
        return _adminUserAppService.GetCustomerSmartDealSalesAsync(membershipId, tenantId, input);
    }

    [HttpGet("memberships/{membershipId:guid}/transactions")]
    public Task<PagedResultDto<TransactionListItemDto>> GetCustomerTransactionsAsync(
        Guid membershipId, [FromQuery] Guid tenantId, [FromQuery] PagedAndSortedResultRequestDto input)
    {
        return _adminUserAppService.GetCustomerTransactionsAsync(membershipId, tenantId, input);
    }
}
