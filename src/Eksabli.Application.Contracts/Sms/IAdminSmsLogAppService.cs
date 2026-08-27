using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Eksabli.Sms;

// Exposed via an explicit controller (src/Eksabli.HttpApi/Controllers/AdminSmsLogsController.cs),
// same "manual controller per app service" shape every other app service in this solution already
// uses — see IAdminAuditLogAppService's own comment for the one place that shape was actually forced
// by a missing package; here it's just this repo's established convention.
[RemoteService(IsEnabled = false)]
public interface IAdminSmsLogAppService : IApplicationService
{
    Task<PagedResultDto<SmsLogDto>> GetListAsync(AdminSmsLogFilterDto input);

    // Dev/testing housekeeping — SmsLog has no automatic expiry (unlike the 5-minute OTP cache itself),
    // so this lets an admin clear accumulated test codes without needing direct DB access.
    Task ClearAsync();
}
