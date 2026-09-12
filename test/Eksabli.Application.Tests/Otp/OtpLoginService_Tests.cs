using System;
using System.Threading.Tasks;
using Eksabli.CustomerProfiles;
using Eksabli.Permissions;
using Microsoft.Extensions.Caching.Distributed;
using Shouldly;
using Volo.Abp.Caching;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Volo.Abp.PermissionManagement;
using Xunit;

namespace Eksabli.Otp;

public abstract class OtpLoginService_Tests<TStartupModule> : EksabliApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IOtpLoginService _otpLoginService;
    private readonly IDistributedCache<OtpCacheItem, string> _otpCache;
    private readonly IRepository<CustomerProfile, Guid> _customerProfileRepository;
    private readonly IPermissionManager _permissionManager;

    protected OtpLoginService_Tests()
    {
        _otpLoginService = GetRequiredService<IOtpLoginService>();
        _otpCache = GetRequiredService<IDistributedCache<OtpCacheItem, string>>();
        _customerProfileRepository = GetRequiredService<IRepository<CustomerProfile, Guid>>();
        _permissionManager = GetRequiredService<IPermissionManager>();
    }

    // Normalized before caching, same as OtpAppService.RequestOtpAsync does in production — the real
    // cache key OtpLoginService.ValidateAndResolveUserAsync looks up is always the normalized number,
    // never the raw one.
    private Task SeedCodeAsync(string phoneNumber, string code) => WithUnitOfWorkAsync(() => _otpCache.SetAsync(
        PhoneNumberNormalizer.Normalize(phoneNumber),
        new OtpCacheItem { Code = code },
        new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) }));

    // Digits only, deliberately — a GUID hex substring can contain a-f, which PhoneNumberNormalizer
    // (correctly) strips as non-digit noise. A phone number containing letters was never realistic test
    // data to begin with, and it silently desynced this helper's raw key from ValidateAndResolveUserAsync's
    // normalized lookup whenever the random GUID happened to land on a hex letter.
    private static string NewPhoneNumber() => "+1555" + Random.Shared.Next(1000000, 10000000);

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

    // Covers the "member web login" permission signal ValidateAndResolveUserAsync grants right before
    // returning (see that method's own comment) — the thing customer-login.component.ts's whole flow
    // exists to obtain for a real, non-mobile-app customer. "U" is ABP's own well-known
    // UserPermissionValueProvider.ProviderName, same literal-string convention
    // EksabliPermissionDefinition_Tests already uses for "R" (role) grants.
    [Fact]
    public async Task Should_Grant_Customer_Permission_On_Successful_Login()
    {
        var phoneNumber = NewPhoneNumber();
        await SeedCodeAsync(phoneNumber, "666666");

        var result = await WithUnitOfWorkAsync(() => _otpLoginService.ValidateAndResolveUserAsync(phoneNumber, "666666"));
        result.IsValid.ShouldBeTrue();

        await WithUnitOfWorkAsync(async () =>
        {
            var grant = await _permissionManager.GetAsync(EksabliPermissions.Customer.Default, "U", result.User!.Id.ToString());
            grant.IsGranted.ShouldBeTrue();
        });
    }

    // The permission grant is unconditional on every successful validation (see
    // ValidateAndResolveUserAsync's own comment on why), not just the two "just proved their phone"
    // branches — so a RETURNING user's second, ordinary login must show it granted too, not only a
    // brand-new user's first one.
    [Fact]
    public async Task Should_Grant_Customer_Permission_On_Returning_Users_Login_Too()
    {
        var phoneNumber = NewPhoneNumber();

        await SeedCodeAsync(phoneNumber, "777777");
        var firstLogin = await WithUnitOfWorkAsync(() => _otpLoginService.ValidateAndResolveUserAsync(phoneNumber, "777777"));
        firstLogin.IsValid.ShouldBeTrue();

        await SeedCodeAsync(phoneNumber, "888888");
        var secondLogin = await WithUnitOfWorkAsync(() => _otpLoginService.ValidateAndResolveUserAsync(phoneNumber, "888888"));
        secondLogin.IsValid.ShouldBeTrue();

        await WithUnitOfWorkAsync(async () =>
        {
            var grant = await _permissionManager.GetAsync(EksabliPermissions.Customer.Default, "U", secondLogin.User!.Id.ToString());
            grant.IsGranted.ShouldBeTrue();
        });
    }
}
