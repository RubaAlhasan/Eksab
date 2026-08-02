using System;
using System.Threading.Tasks;
using Eksabli.Campaigns;
using Eksabli.Permissions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;

namespace Eksabli.Controllers;

[ApiController]
[Route("api/app/campaign")]
[Authorize(EksabliPermissions.Campaigns.Default)]
public class CampaignsController : EksabliController
{
    private readonly ICampaignAppService _campaignAppService;

    public CampaignsController(ICampaignAppService campaignAppService)
    {
        _campaignAppService = campaignAppService;
    }

    [HttpGet("{id}")]
    public Task<CampaignDto> GetAsync(Guid id)
    {
        return _campaignAppService.GetAsync(id);
    }

    [HttpGet]
    public Task<PagedResultDto<CampaignDto>> GetListAsync([FromQuery] PagedAndSortedResultRequestDto input)
    {
        return _campaignAppService.GetListAsync(input);
    }

    [Authorize(EksabliPermissions.Campaigns.Create)]
    [HttpPost]
    public Task<CampaignDto> CreateAsync(CreateUpdateCampaignDto input)
    {
        return _campaignAppService.CreateAsync(input);
    }

    [Authorize(EksabliPermissions.Campaigns.Edit)]
    [HttpPut("{id}")]
    public Task<CampaignDto> UpdateAsync(Guid id, CreateUpdateCampaignDto input)
    {
        return _campaignAppService.UpdateAsync(id, input);
    }

    [Authorize(EksabliPermissions.Campaigns.Edit)]
    [HttpDelete("{id}")]
    public Task DeleteAsync(Guid id)
    {
        return _campaignAppService.DeleteAsync(id);
    }

    [Authorize(EksabliPermissions.Campaigns.Activate)]
    [HttpPost("{id}/activate")]
    public Task<CampaignDto> ActivateAsync(Guid id)
    {
        return _campaignAppService.ActivateAsync(id);
    }

    [HttpGet("{id}/target-segment-preview")]
    public Task<TargetSegmentPreviewDto> PreviewTargetSegmentAsync(Guid id)
    {
        return _campaignAppService.PreviewTargetSegmentAsync(id);
    }
}
