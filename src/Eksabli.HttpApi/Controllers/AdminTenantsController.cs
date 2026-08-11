using System;
using System.Threading.Tasks;
using Eksabli.Businesses;
using Eksabli.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;

namespace Eksabli.Controllers;

[ApiController]
[Route("api/app/admin-tenants")]
[Authorize(EksabliPermissions.Tenants.View)]
public class AdminTenantsController : EksabliController
{
    private readonly IAdminTenantAppService _adminTenantAppService;

    public AdminTenantsController(IAdminTenantAppService adminTenantAppService)
    {
        _adminTenantAppService = adminTenantAppService;
    }

    [HttpGet]
    public Task<PagedResultDto<AdminTenantDto>> GetListAsync([FromQuery] AdminTenantFilterDto input)
    {
        return _adminTenantAppService.GetListAsync(input);
    }

    [HttpGet("{tenantId}")]
    public Task<AdminTenantDto> GetAsync(Guid tenantId)
    {
        return _adminTenantAppService.GetAsync(tenantId);
    }

    [HttpGet("{tenantId}/detail-stats")]
    public Task<AdminTenantDetailStatsDto> GetDetailStatsAsync(Guid tenantId)
    {
        return _adminTenantAppService.GetDetailStatsAsync(tenantId);
    }

    [Authorize(EksabliPermissions.Tenants.Approve)]
    [HttpPost("{tenantId}/approve")]
    public Task<AdminTenantDto> ApproveAsync(Guid tenantId)
    {
        return _adminTenantAppService.ApproveAsync(tenantId);
    }

    [Authorize(EksabliPermissions.Tenants.Suspend)]
    [HttpPost("{tenantId}/suspend")]
    public Task<AdminTenantDto> SuspendAsync(Guid tenantId)
    {
        return _adminTenantAppService.SuspendAsync(tenantId);
    }
}
