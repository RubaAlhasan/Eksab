using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Eksabli.Businesses;

// Host-realm, platform operations — exposed via an explicit controller
// (src/Eksabli.HttpApi/Controllers/AdminTenantsController.cs). Spans every tenant, mirrors the
// realm-crossing shape of Billing.IAdminSubscriptionAppService.
[RemoteService(IsEnabled = false)]
public interface IAdminTenantAppService : IApplicationService
{
    Task<PagedResultDto<AdminTenantDto>> GetListAsync(AdminTenantFilterDto input);

    Task<AdminTenantDto> GetAsync(Guid tenantId);

    Task<AdminTenantDetailStatsDto> GetDetailStatsAsync(Guid tenantId);

    Task<AdminTenantDto> ApproveAsync(Guid tenantId);

    Task<AdminTenantDto> SuspendAsync(Guid tenantId);
}
