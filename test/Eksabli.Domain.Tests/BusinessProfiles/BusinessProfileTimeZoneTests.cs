using System;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace Eksabli.BusinessProfiles;

public class BusinessProfileTimeZoneTests
{
    [Fact]
    public void A_New_Profile_Starts_In_The_Default_Zone()
    {
        var profile = BusinessProfile.Create(Guid.NewGuid());

        profile.TimeZoneId.ShouldBe(BusinessProfileConsts.DefaultTimeZoneId);
        profile.ResolveTimeZone().Id.ShouldBe(BusinessProfileConsts.DefaultTimeZoneId);
    }

    [Fact]
    public void SetTimeZone_Should_Accept_A_Real_IANA_Zone()
    {
        var profile = BusinessProfile.Create(Guid.NewGuid());

        profile.SetTimeZone("Europe/Istanbul");

        profile.TimeZoneId.ShouldBe("Europe/Istanbul");
    }

    [Fact]
    public void SetTimeZone_Should_Reject_An_Unknown_Zone_And_Keep_The_Previous_One()
    {
        var profile = BusinessProfile.Create(Guid.NewGuid());

        Should.Throw<UserFriendlyException>(() => profile.SetTimeZone("Not/A_Zone"));

        profile.TimeZoneId.ShouldBe(BusinessProfileConsts.DefaultTimeZoneId);
    }

    [Fact]
    public void SetTimeZone_Should_Reject_A_Blank_Zone()
    {
        var profile = BusinessProfile.Create(Guid.NewGuid());

        Should.Throw<ArgumentException>(() => profile.SetTimeZone("   "));
    }
}
