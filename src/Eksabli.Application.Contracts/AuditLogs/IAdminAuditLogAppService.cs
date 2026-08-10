using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Eksabli.AuditLogs;

// Exposed via an explicit controller (src/Eksabli.HttpApi/Controllers/AuditLogsController.cs).
//
// ABP's OSS `Volo.Abp.AuditLogging.Domain`/`.EntityFrameworkCore` packages (already referenced —
// EksabliDbContext already calls builder.ConfigureAuditLogging(), and the AbpAuditLogs table already
// exists from the very first migration) record every request's audit log automatically via ABP's
// built-in interceptors — that data has been live since day one. What's genuinely missing is a way to
// QUERY it: the browsable Application/HttpApi/Angular UI layer ABP ships for other modules
// (Identity/TenantManagement/SettingManagement) does NOT exist for AuditLogging in the free/OSS
// package family — `Volo.Abp.AuditLogging.Application[.Contracts]`/`.HttpApi` do not exist on
// nuget.org at all (confirmed directly against the NuGet Search API); that layer is exclusively part
// of ABP Commercial, a paid product this repo has no license/feed configured for (see
// NEXT_SESSION_PROMPT.md's own note on this investigation). This is a small, hand-written query
// surface over the real `IAuditLogRepository` (Volo.Abp.AuditLogging.Domain) instead — real data, no
// commercial dependency.
[RemoteService(IsEnabled = false)]
public interface IAdminAuditLogAppService : IApplicationService
{
    Task<PagedResultDto<AuditLogDto>> GetListAsync(AdminAuditLogFilterDto input);

    Task<AuditLogDetailDto> GetAsync(Guid id);
}
