using System.Threading.Tasks;
using Eksabli.Permissions;
using Eksabli.Sms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;

namespace Eksabli.Controllers;

[ApiController]
[Route("api/app/admin-sms-logs")]
[Authorize(EksabliPermissions.SmsLogs.Default)]
public class AdminSmsLogsController : EksabliController
{
    private readonly IAdminSmsLogAppService _adminSmsLogAppService;

    public AdminSmsLogsController(IAdminSmsLogAppService adminSmsLogAppService)
    {
        _adminSmsLogAppService = adminSmsLogAppService;
    }

    [HttpGet]
    public Task<PagedResultDto<SmsLogDto>> GetListAsync([FromQuery] AdminSmsLogFilterDto input)
    {
        return _adminSmsLogAppService.GetListAsync(input);
    }

    [HttpDelete]
    public Task ClearAsync()
    {
        return _adminSmsLogAppService.ClearAsync();
    }
}
