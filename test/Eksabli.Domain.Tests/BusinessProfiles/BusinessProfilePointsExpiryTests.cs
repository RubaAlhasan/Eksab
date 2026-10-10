using System;
using Shouldly;
using Xunit;

namespace Eksabli.BusinessProfiles;

public class BusinessProfilePointsExpiryTests
{
    private static readonly DateTime EarnedAt = new(2026, 10, 5, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void A_New_Profile_Never_Expires_Points()
    {
        var profile = BusinessProfile.Create(Guid.NewGuid());

        profile.PointsExpiryMonths.ShouldBeNull();
        profile.ComputeExpiresAt(EarnedAt).ShouldBeNull();
    }

    [Fact]
    public void ComputeExpiresAt_Should_Add_The_Configured_Months_To_The_Award_Time()
    {
        var profile = BusinessProfile.Create(Guid.NewGuid());

        profile.SetPointsExpiryMonths(12);

        profile.ComputeExpiresAt(EarnedAt).ShouldBe(new DateTime(2027, 10, 5, 9, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void Clearing_The_Setting_Should_Switch_Expiry_Off()
    {
        var profile = BusinessProfile.Create(Guid.NewGuid());
        profile.SetPointsExpiryMonths(6);

        profile.SetPointsExpiryMonths(null);

        profile.PointsExpiryMonths.ShouldBeNull();
        profile.ComputeExpiresAt(EarnedAt).ShouldBeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(121)]
    public void SetPointsExpiryMonths_Should_Reject_Values_Outside_The_Allowed_Range_And_Keep_The_Previous_One(int months)
    {
        var profile = BusinessProfile.Create(Guid.NewGuid());
        profile.SetPointsExpiryMonths(12);

        Should.Throw<ArgumentException>(() => profile.SetPointsExpiryMonths(months));

        profile.PointsExpiryMonths.ShouldBe(12);
    }
}
