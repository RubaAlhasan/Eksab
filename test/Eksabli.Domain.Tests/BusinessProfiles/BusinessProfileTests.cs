using System;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace Eksabli.BusinessProfiles;

public class BusinessProfileTests
{
    [Fact]
    public void Create_Should_Start_Pending()
    {
        var profile = BusinessProfile.Create(Guid.NewGuid());

        profile.ApprovalStatus.ShouldBe(TenantApprovalStatus.Pending);
    }

    [Fact]
    public void Approve_Should_Transition_Pending_To_Approved()
    {
        var profile = BusinessProfile.Create(Guid.NewGuid());

        profile.Approve();

        profile.ApprovalStatus.ShouldBe(TenantApprovalStatus.Approved);
    }

    [Fact]
    public void Approve_Should_Throw_When_Already_Approved()
    {
        var profile = BusinessProfile.Create(Guid.NewGuid());
        profile.Approve();

        Should.Throw<UserFriendlyException>(() => profile.Approve());
    }

    [Fact]
    public void Suspend_Should_Transition_Approved_To_Suspended()
    {
        var profile = BusinessProfile.Create(Guid.NewGuid());
        profile.Approve();

        profile.Suspend();

        profile.ApprovalStatus.ShouldBe(TenantApprovalStatus.Suspended);
    }

    [Fact]
    public void Approve_After_Suspend_Should_Reinstate()
    {
        var profile = BusinessProfile.Create(Guid.NewGuid());
        profile.Approve();
        profile.Suspend();

        profile.Approve();

        profile.ApprovalStatus.ShouldBe(TenantApprovalStatus.Approved);
    }
}
