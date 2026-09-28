using System;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Modularity;
using Xunit;

namespace Eksabli.Campaigns;

public abstract class CampaignAppService_Tests<TStartupModule> : EksabliApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly ICampaignAppService _campaignAppService;

    protected CampaignAppService_Tests()
    {
        _campaignAppService = GetRequiredService<ICampaignAppService>();
    }

    [Fact]
    public async Task Should_Create_List_Update_And_Delete_A_Campaign()
    {
        var created = await WithUnitOfWorkAsync(() => _campaignAppService.CreateAsync(new CreateUpdateCampaignDto
        {
            NameAr = "نقاط مضاعفة",
            NameEn = "Double Points Weekend",
            Type = CampaignType.DoublePoints,
            RulesJson = "{\"multiplier\":2}",
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(2)
        }));
        created.Status.ShouldBe(CampaignStatus.Draft);

        var list = await WithUnitOfWorkAsync(() => _campaignAppService.GetListAsync(new PagedAndSortedResultRequestDto()));
        list.Items.ShouldContain(c => c.Id == created.Id);

        var updated = await WithUnitOfWorkAsync(() => _campaignAppService.UpdateAsync(created.Id, new CreateUpdateCampaignDto
        {
            NameAr = "نقاط مضاعفة",
            NameEn = "Double Points Weekend",
            Type = CampaignType.DoublePoints,
            RulesJson = "{\"multiplier\":3}",
            StartDate = created.StartDate,
            EndDate = created.EndDate
        }));
        updated.RulesJson.ShouldBe("{\"multiplier\":3}");

        await WithUnitOfWorkAsync(() => _campaignAppService.DeleteAsync(created.Id));
        var afterDelete = await WithUnitOfWorkAsync(() => _campaignAppService.GetListAsync(new PagedAndSortedResultRequestDto()));
        afterDelete.Items.ShouldNotContain(c => c.Id == created.Id);
    }

    [Fact]
    public async Task Should_Activate_A_Draft_Campaign_Once()
    {
        var created = await WithUnitOfWorkAsync(() => _campaignAppService.CreateAsync(new CreateUpdateCampaignDto
        {
            NameAr = "استعادة العملاء",
            NameEn = "Win-back",
            Type = CampaignType.WinBack,
            RulesJson = "{\"bonusPoints\":50}",
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(30),
            TargetRules =
            {
                new CreateUpdateCampaignTargetRuleDto { SegmentType = CampaignTargetRuleSegmentType.Inactive, ParametersJson = "{\"inactiveDays\":30}" }
            }
        }));

        var activated = await WithUnitOfWorkAsync(() => _campaignAppService.ActivateAsync(created.Id));
        activated.Status.ShouldBe(CampaignStatus.Active);
        activated.TargetRules.ShouldHaveSingleItem();

        await Should.ThrowAsync<Exception>(() => WithUnitOfWorkAsync(() => _campaignAppService.ActivateAsync(created.Id)));
    }

    [Fact]
    public async Task PreviewTargetSegment_Should_Return_Zero_For_A_Campaign_Without_TargetRules()
    {
        var created = await WithUnitOfWorkAsync(() => _campaignAppService.CreateAsync(new CreateUpdateCampaignDto
        {
            NameAr = "كبار العملاء",
            NameEn = "VIP",
            Type = CampaignType.Vip,
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(30)
        }));

        var preview = await WithUnitOfWorkAsync(() => _campaignAppService.PreviewTargetSegmentAsync(created.Id));

        preview.MatchedMembershipCount.ShouldBe(0);
    }

    // Regression test for a real production bug: a "Spend X, Get Y" campaign was saved and Activated
    // with Bonus Points left blank. It showed as Active and in its date range, but
    // CampaignRulesEngine.EvaluateAsync's own "candidateBonus > bonusPoints" comparison (which starts at
    // 0) never let a 0 bonus win, so every POS sale it should have topped up silently earned nothing.
    [Fact]
    public async Task Should_Not_Activate_A_SpendXGetY_Campaign_With_No_Bonus_Points()
    {
        var created = await WithUnitOfWorkAsync(() => _campaignAppService.CreateAsync(new CreateUpdateCampaignDto
        {
            NameAr = "أنفق واحصل",
            NameEn = "Spend X Get Y",
            Type = CampaignType.SpendXGetY,
            RulesJson = "{\"spendThreshold\":100}", // BonusPoints left unset — the exact bug
            StartDate = DateTime.UtcNow,
            EndDate = DateTime.UtcNow.AddDays(30)
        }));

        await Should.ThrowAsync<Exception>(() => WithUnitOfWorkAsync(() => _campaignAppService.ActivateAsync(created.Id)));
    }
}
