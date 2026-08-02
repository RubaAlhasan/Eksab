using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Eksabli.Campaigns;

// Exposed via an explicit controller (src/Eksabli.HttpApi/Controllers/CampaignsController.cs).
[RemoteService(IsEnabled = false)]
public interface ICampaignAppService : IApplicationService
{
    Task<CampaignDto> GetAsync(Guid id);

    Task<PagedResultDto<CampaignDto>> GetListAsync(PagedAndSortedResultRequestDto input);

    Task<CampaignDto> CreateAsync(CreateUpdateCampaignDto input);

    Task<CampaignDto> UpdateAsync(Guid id, CreateUpdateCampaignDto input);

    Task DeleteAsync(Guid id);

    Task<CampaignDto> ActivateAsync(Guid id);

    Task<TargetSegmentPreviewDto> PreviewTargetSegmentAsync(Guid id);
}
