using System;
using Eksabli.Campaigns;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace Eksabli.Campaigns;

// Pure entity-behavior tests — no DB/DI needed, mirrors Eksabli.Billing.TenantSubscriptionTests.
public class CampaignTests
{
    [Fact]
    public void Activate_Should_Transition_Draft_To_Active()
    {
        var campaign = Campaign.Create(Guid.NewGuid(), "حملة", "Campaign", CampaignType.DoublePoints, DateTime.UtcNow, DateTime.UtcNow.AddDays(7));

        campaign.Activate();

        campaign.Status.ShouldBe(CampaignStatus.Active);
    }

    [Fact]
    public void Activate_Should_Throw_When_Not_Draft()
    {
        var campaign = Campaign.Create(Guid.NewGuid(), "حملة", "Campaign", CampaignType.DoublePoints, DateTime.UtcNow, DateTime.UtcNow.AddDays(7));
        campaign.Activate();

        Should.Throw<UserFriendlyException>(() => campaign.Activate());
    }

    [Fact]
    public void End_Should_Transition_Active_To_Ended()
    {
        var campaign = Campaign.Create(Guid.NewGuid(), "حملة", "Campaign", CampaignType.DoublePoints, DateTime.UtcNow, DateTime.UtcNow.AddDays(7));
        campaign.Activate();

        campaign.End();

        campaign.Status.ShouldBe(CampaignStatus.Ended);
    }

    [Fact]
    public void End_Should_Be_A_NoOp_When_Draft()
    {
        var campaign = Campaign.Create(Guid.NewGuid(), "حملة", "Campaign", CampaignType.DoublePoints, DateTime.UtcNow, DateTime.UtcNow.AddDays(7));

        campaign.End();

        campaign.Status.ShouldBe(CampaignStatus.Draft);
    }

    [Fact]
    public void Create_Should_Throw_When_EndDate_Not_After_StartDate()
    {
        var now = DateTime.UtcNow;

        Should.Throw<UserFriendlyException>(() =>
            Campaign.Create(Guid.NewGuid(), "حملة", "Campaign", CampaignType.Birthday, now, now));
    }

    [Fact]
    public void AddTargetRule_Should_Append_To_TargetRules()
    {
        var campaign = Campaign.Create(Guid.NewGuid(), "حملة", "Campaign", CampaignType.WinBack, DateTime.UtcNow, DateTime.UtcNow.AddDays(7));

        campaign.AddTargetRule(Guid.NewGuid(), CampaignTargetRuleSegmentType.Inactive, "{\"InactiveDays\":30}");

        campaign.TargetRules.Count.ShouldBe(1);
        campaign.TargetRules.ShouldContain(r => r.SegmentType == CampaignTargetRuleSegmentType.Inactive);
    }

    [Fact]
    public void ClearTargetRules_Should_Empty_The_Collection()
    {
        var campaign = Campaign.Create(Guid.NewGuid(), "حملة", "Campaign", CampaignType.WinBack, DateTime.UtcNow, DateTime.UtcNow.AddDays(7));
        campaign.AddTargetRule(Guid.NewGuid(), CampaignTargetRuleSegmentType.All, null);

        campaign.ClearTargetRules();

        campaign.TargetRules.ShouldBeEmpty();
    }
}

public class CampaignRulesTests
{
    [Fact]
    public void Parse_Should_Return_Defaults_For_Null_Or_Empty()
    {
        CampaignRules.Parse(null).Multiplier.ShouldBeNull();
        CampaignRules.Parse(string.Empty).BonusPoints.ShouldBeNull();
    }

    [Fact]
    public void Parse_Should_Return_Defaults_For_Malformed_Json()
    {
        var rules = CampaignRules.Parse("{not-json");

        rules.Multiplier.ShouldBeNull();
        rules.BonusPoints.ShouldBeNull();
    }

    [Fact]
    public void Parse_Should_Read_Known_Fields()
    {
        var rules = CampaignRules.Parse("{\"multiplier\":2.5,\"bonusPoints\":50,\"spendThreshold\":100,\"daysBefore\":3}");

        rules.Multiplier.ShouldBe(2.5m);
        rules.BonusPoints.ShouldBe(50);
        rules.SpendThreshold.ShouldBe(100m);
        rules.DaysBefore.ShouldBe(3);
    }
}
