using System.Threading.Tasks;
using Eksabli.AuditLogs;
using Eksabli.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;

namespace Eksabli.Controllers;

[ApiController]
[Route("api/app/audit-log")]
[Authorize(EksabliPermissions.AuditLogs.Default)]
public class AuditLogsController : EksabliController
{
    private readonly IAdminAuditLogAppService _adminAuditLogAppService;

    public AuditLogsController(IAdminAuditLogAppService adminAuditLogAppService)
    {
        _adminAuditLogAppService = adminAuditLogAppService;
    }

    [HttpGet]
    public Task<PagedResultDto<AuditLogDto>> GetListAsync([FromQuery] AdminAuditLogFilterDto input)
    {
        return _adminAuditLogAppService.GetListAsync(input);
    }
}
