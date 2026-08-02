using System;
using Eksabli.Engagement;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace Eksabli.Engagement;

// Pure entity-behavior tests — no DB/DI needed, mirrors Eksabli.Campaigns.CampaignTests.
public class ReferralTests
{
    [Fact]
    public void Create_Should_Start_As_Pending()
    {
        var referral = Referral.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        referral.Status.ShouldBe(ReferralStatus.Pending);
    }

    [Fact]
    public void Complete_Should_Transition_Pending_To_Completed()
    {
        var referral = Referral.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        referral.Complete();

        referral.Status.ShouldBe(ReferralStatus.Completed);
    }

    [Fact]
    public void Complete_Should_Throw_When_Not_Pending()
    {
        var referral = Referral.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        referral.Complete();

        Should.Throw<UserFriendlyException>(() => referral.Complete());
    }

    [Fact]
    public void MarkRewarded_Should_Transition_Completed_To_Rewarded()
    {
        var referral = Referral.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        referral.Complete();

        referral.MarkRewarded();

        referral.Status.ShouldBe(ReferralStatus.Rewarded);
    }

    [Fact]
    public void MarkRewarded_Should_Throw_When_Not_Completed()
    {
        var referral = Referral.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        Should.Throw<UserFriendlyException>(() => referral.MarkRewarded());
    }
}

public class AchievementTests
{
    [Fact]
    public void Create_Should_Allow_Null_TenantId_For_PlatformWide_Badges()
    {
        var achievement = Achievement.Create(Guid.NewGuid(), null, "Regular");

        achievement.TenantId.ShouldBeNull();
        achievement.Name.ShouldBe("Regular");
    }

    [Fact]
    public void SetCriteria_Should_Update_CriteriaJson()
    {
        var achievement = Achievement.Create(Guid.NewGuid(), Guid.NewGuid(), "Frequent Visitor");

        achievement.SetCriteria("{\"visits\":10}");

        achievement.CriteriaJson.ShouldBe("{\"visits\":10}");
    }
}
