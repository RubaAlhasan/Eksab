using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Eksabli.Platform;

// Host-realm, platform operations — exposed via an explicit controller
// (src/Eksabli.HttpApi/Controllers/AdminUsersController.cs). Spans both realms (Host-realm customers +
// every tenant's business staff), same cross-cutting shape as Businesses.IAdminTenantAppService.
[RemoteService(IsEnabled = false)]
public interface IAdminUserAppService : IApplicationService
{
    Task<PagedResultDto<AdminUserDto>> GetListAsync(AdminUserFilterDto input);
}
