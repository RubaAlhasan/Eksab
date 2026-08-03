using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Eksabli.Features;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Features;

namespace Eksabli.Campaigns;

[RemoteService(IsEnabled = false)]
public class CampaignAppService : ApplicationService, ICampaignAppService
{
    private readonly ICampaignRepository _repository;
    private readonly ICampaignSegmentEvaluator _segmentEvaluator;

    public CampaignAppService(ICampaignRepository repository, ICampaignSegmentEvaluator segmentEvaluator)
    {
        _repository = repository;
        _segmentEvaluator = segmentEvaluator;
    }

    public async Task<CampaignDto> GetAsync(Guid id)
    {
        var campaign = await _repository.GetAsync(id);
        return MapWithTargetRules(campaign);
    }

    public async Task<PagedResultDto<CampaignDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var (campaigns, totalCount) = await _repository.GetListAsync(
            sorting: input.Sorting,
            skipCount: input.SkipCount,
            maxResultCount: input.MaxResultCount);

        return new PagedResultDto<CampaignDto>(totalCount, campaigns.Select(MapWithTargetRules).ToList());
    }

    public async Task<CampaignDto> CreateAsync(CreateUpdateCampaignDto input)
    {
        // Plan-limit enforcement via ABP Feature Management, not new business logic — see
        // docs/eksabli-loyalty-platform/features/04-billing-subscriptions/README.md.
        var maxCampaigns = await FeatureChecker.GetAsync<int>(EksabliFeatures.MaxCampaigns);
        var (_, activeCount) = await _repository.GetListAsync(status: CampaignStatus.Active);
        if (activeCount >= maxCampaigns)
        {
            throw new UserFriendlyException("You've reached the active campaign limit for your current plan. Upgrade to run more campaigns.");
        }

        var campaign = Campaign.Create(GuidGenerator.Create(), input.NameAr, input.NameEn, input.Type, input.StartDate, input.EndDate);
        campaign.SetRules(input.RulesJson);
        ApplyTargetRules(campaign, input.TargetRules);

        await _repository.InsertAsync(campaign);
        return MapWithTargetRules(campaign);
    }

    public async Task<CampaignDto> UpdateAsync(Guid id, CreateUpdateCampaignDto input)
    {
        var campaign = await _repository.GetAsync(id);
        campaign.SetNames(input.NameAr, input.NameEn);
        campaign.SetType(input.Type);
        campaign.SetRules(input.RulesJson);
        campaign.SetDateRange(input.StartDate, input.EndDate);

        campaign.ClearTargetRules();
        ApplyTargetRules(campaign, input.TargetRules);

        await _repository.UpdateAsync(campaign);
        return MapWithTargetRules(campaign);
    }

    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }

    public async Task<CampaignDto> ActivateAsync(Guid id)
    {
        var campaign = await _repository.GetAsync(id);
        campaign.Activate();
        await _repository.UpdateAsync(campaign);
        return MapWithTargetRules(campaign);
    }

    public async Task<TargetSegmentPreviewDto> PreviewTargetSegmentAsync(Guid id)
    {
        var campaign = await _repository.GetAsync(id);
        var matched = await _segmentEvaluator.EvaluateAsync(campaign);
        return new TargetSegmentPreviewDto { MatchedMembershipCount = matched.Count };
    }

    private void ApplyTargetRules(Campaign campaign, List<CreateUpdateCampaignTargetRuleDto> targetRules)
    {
        foreach (var rule in targetRules)
        {
            campaign.AddTargetRule(GuidGenerator.Create(), rule.SegmentType, rule.ParametersJson);
        }
    }

    private CampaignDto MapWithTargetRules(Campaign campaign)
    {
        var dto = ObjectMapper.Map<Campaign, CampaignDto>(campaign);
        dto.TargetRules = campaign.TargetRules
            .Select(r => ObjectMapper.Map<CampaignTargetRule, CampaignTargetRuleDto>(r))
            .ToList();
        return dto;
    }
}
