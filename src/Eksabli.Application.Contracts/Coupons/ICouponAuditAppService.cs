using System.Threading.Tasks;
using Eksabli.Shared;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Content;

namespace Eksabli.Rewards;

// Exposed via an explicit controller (src/Eksabli.HttpApi/Controllers/CouponAuditController.cs).
[RemoteService(IsEnabled = false)]
public interface ICouponAuditAppService : IApplicationService
{
    Task<PagedResultDto<CouponDto>> GetListAsync(CouponAuditFilterDto input);

    Task<IRemoteStreamContent> GetListAsExcelFileAsync(CouponExcelDownloadDto input);

    Task<DownloadTokenResultDto> GetDownloadTokenAsync();
}
