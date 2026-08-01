using System;
using System.Threading.Tasks;
using Eksabli.CustomerProfiles;
using Microsoft.Extensions.Caching.Distributed;
using Shouldly;
using Volo.Abp.Caching;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Xunit;

namespace Eksabli.Otp;

public abstract class OtpLoginService_Tests<TStartupModule> : EksabliApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IOtpLoginService _otpLoginService;
    private readonly IDistributedCache<OtpCacheItem, string> _otpCache;
    private readonly IRepository<CustomerProfile, Guid> _customerProfileRepository;

    protected OtpLoginService_Tests()
    {
        _otpLoginService = GetRequiredService<IOtpLoginService>();
        _otpCache = GetRequiredService<IDistributedCache<OtpCacheItem, string>>();
        _customerProfileRepository = GetRequiredService<IRepository<CustomerProfile, Guid>>();
    }

    private Task SeedCodeAsync(string phoneNumber, string code) => WithUnitOfWorkAsync(() => _otpCache.SetAsync(
        phoneNumber,
        new OtpCacheItem { Code = code },
        new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) }));

    private static string NewPhoneNumber() => "+1555" + Guid.NewGuid().ToString("N").Substring(0, 7);

    [Fact]
    public async Task Should_Reject_When_No_Code_Requested()
    {
        var result = await WithUnitOfWorkAsync(() => _otpLoginService.ValidateAndResolveUserAsync(NewPhoneNumber(), "123456"));

        result.IsValid.ShouldBeFalse();
        result.ErrorCode.ShouldBe("expired_code");
    }

    [Fact]
    public async Task Should_Reject_Wrong_Code_Without_Burning_The_Real_Code()
    {
        var phoneNumber = NewPhoneNumber();
        await SeedCodeAsync(phoneNumber, "111111");

        var wrongAttempt = await WithUnitOfWorkAsync(() => _otpLoginService.ValidateAndResolveUserAsync(phoneNumber, "000000"));
        wrongAttempt.IsValid.ShouldBeFalse();
        wrongAttempt.ErrorCode.ShouldBe("invalid_code");

        // The real code must still work after a wrong guess — a mistyped digit shouldn't force a new SMS round-trip.
        var correctAttempt = await WithUnitOfWorkAsync(() => _otpLoginService.ValidateAndResolveUserAsync(phoneNumber, "111111"));
        correctAttempt.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_Create_New_User_And_CustomerProfile_On_First_Valid_Code()
    {
        var phoneNumber = NewPhoneNumber();
        await SeedCodeAsync(phoneNumber, "222222");

        var result = await WithUnitOfWorkAsync(() => _otpLoginService.ValidateAndResolveUserAsync(phoneNumber, "222222"));

        result.IsValid.ShouldBeTrue();
        result.IsNewUser.ShouldBeTrue();
        result.User.ShouldNotBeNull();
        result.User!.PhoneNumber.ShouldBe(phoneNumber);
        result.User.TenantId.ShouldBeNull();

        await WithUnitOfWorkAsync(async () =>
        {
            var profile = await _customerProfileRepository.FirstOrDefaultAsync(x => x.UserId == result.User!.Id);
            profile.ShouldNotBeNull();
        });
    }

    [Fact]
    public async Task Should_Reuse_Existing_User_On_Second_Login()
    {
        var phoneNumber = NewPhoneNumber();

        await SeedCodeAsync(phoneNumber, "333333");
        var firstLogin = await WithUnitOfWorkAsync(() => _otpLoginService.ValidateAndResolveUserAsync(phoneNumber, "333333"));
        firstLogin.IsNewUser.ShouldBeTrue();

        await SeedCodeAsync(phoneNumber, "444444");
        var secondLogin = await WithUnitOfWorkAsync(() => _otpLoginService.ValidateAndResolveUserAsync(phoneNumber, "444444"));

        secondLogin.IsValid.ShouldBeTrue();
        secondLogin.IsNewUser.ShouldBeFalse();
        secondLogin.User!.Id.ShouldBe(firstLogin.User!.Id);
    }

    [Fact]
    public async Task Should_Be_Single_Use()
    {
        var phoneNumber = NewPhoneNumber();
        await SeedCodeAsync(phoneNumber, "555555");

        var firstAttempt = await WithUnitOfWorkAsync(() => _otpLoginService.ValidateAndResolveUserAsync(phoneNumber, "555555"));
        firstAttempt.IsValid.ShouldBeTrue();

        var secondAttempt = await WithUnitOfWorkAsync(() => _otpLoginService.ValidateAndResolveUserAsync(phoneNumber, "555555"));
        secondAttempt.IsValid.ShouldBeFalse();
        secondAttempt.ErrorCode.ShouldBe("expired_code");
    }
}
