using System.Threading.Tasks;
using Eksabli.Permissions;
using Eksabli.Platform;
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
}
