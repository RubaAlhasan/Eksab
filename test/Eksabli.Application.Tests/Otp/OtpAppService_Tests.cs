using System;
using System.Threading.Tasks;
using Eksabli.CustomerProfiles;
using Microsoft.AspNetCore.Identity;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Xunit;

namespace Eksabli.Otp;

// RegisterAsync's two uniqueness checks (phone number, email) — see that method's own comments for the
// full reasoning, especially why the email check is gated on PhoneNumberConfirmed rather than
// EmailConfirmed (this app never sets EmailConfirmed at all).
public abstract class OtpAppService_Tests<TStartupModule> : EksabliApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IOtpAppService _otpAppService;
    private readonly IdentityUserManager _identityUserManager;
    private readonly IRepository<CustomerProfile, Guid> _customerProfileRepository;
    private readonly ICurrentTenant _currentTenant;

    protected OtpAppService_Tests()
    {
        _otpAppService = GetRequiredService<IOtpAppService>();
        _identityUserManager = GetRequiredService<IdentityUserManager>();
        _customerProfileRepository = GetRequiredService<IRepository<CustomerProfile, Guid>>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
    }

    // Digits only — see OtpLoginService_Tests.NewPhoneNumber's own comment on why (a hex GUID substring
    // can contain a-f, which PhoneNumberNormalizer strips as non-digit noise).
    private static string NewPhoneNumber() => "+1555" + Random.Shared.Next(1000000, 10000000);

    private static RegisterCustomerDto NewRegisterDto(string phoneNumber, string? email = null) => new()
    {
        PhoneNumber = phoneNumber,
        FirstName = "New",
        LastName = "Customer",
        Email = email,
        Password = "P@ssw0rd1",
    };

    // Bypasses the OTP round-trip entirely (SetPhoneNumber(confirmed: true) directly) to seed a fully
    // "real" customer account, the same shape OtpLoginService.ValidateAndResolveUserAsync produces once
    // a code is actually verified — this suite is testing RegisterAsync's own uniqueness checks, not the
    // verify step itself (that's OtpLoginService_Tests's job).
    private async Task CreateConfirmedCustomerAsync(string phoneNumber, string email)
    {
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(null))
            {
                var normalized = PhoneNumberNormalizer.Normalize(phoneNumber);
                var user = new IdentityUser(Guid.NewGuid(), normalized, email, tenantId: null);
                user.SetPhoneNumber(normalized, confirmed: true);
                (await _identityUserManager.CreateAsync(user)).CheckErrors();

                var profile = CustomerProfile.Create(Guid.NewGuid(), user.Id);
                profile.SetName("Existing", "Customer");
                await _customerProfileRepository.InsertAsync(profile, autoSave: true);
            }
        });
    }

    [Fact]
    public async Task Should_Reject_Registration_With_An_Already_Confirmed_Phone_Number()
    {
        var phoneNumber = NewPhoneNumber();
        await CreateConfirmedCustomerAsync(phoneNumber, $"{Guid.NewGuid():N}@example.com");

        var ex = await Assert.ThrowsAsync<UserFriendlyException>(() =>
            WithUnitOfWorkAsync(() => _otpAppService.RegisterAsync(NewRegisterDto(phoneNumber))));

        ex.Message.ShouldContain("phone number");
    }

    [Fact]
    public async Task Should_Reject_Registration_With_An_Already_Confirmed_Email_Even_With_A_Different_Phone()
    {
        var email = $"{Guid.NewGuid():N}@example.com";
        await CreateConfirmedCustomerAsync(NewPhoneNumber(), email);

        var ex = await Assert.ThrowsAsync<UserFriendlyException>(() =>
            WithUnitOfWorkAsync(() => _otpAppService.RegisterAsync(NewRegisterDto(NewPhoneNumber(), email))));

        ex.Message.ShouldContain("email");
    }

    [Fact]
    public async Task Should_Allow_Registration_When_The_Only_Existing_PhoneNumber_Match_Was_Never_Confirmed()
    {
        var phoneNumber = NewPhoneNumber();
        // Start (but never finish — no OTP verify) a first registration attempt for this number.
        await WithUnitOfWorkAsync(() => _otpAppService.RegisterAsync(NewRegisterDto(phoneNumber)));

        // Retrying with the same, still-unconfirmed number must succeed, not be rejected as a duplicate.
        await Should.NotThrowAsync(() => WithUnitOfWorkAsync(() => _otpAppService.RegisterAsync(NewRegisterDto(phoneNumber))));
    }

    [Fact]
    public async Task Should_Allow_Registration_When_The_Only_Existing_Email_Match_Was_Never_Confirmed()
    {
        var email = $"{Guid.NewGuid():N}@example.com";
        // Start (but never finish) a registration under a DIFFERENT phone number, using this email.
        await WithUnitOfWorkAsync(() => _otpAppService.RegisterAsync(NewRegisterDto(NewPhoneNumber(), email)));

        // A different phone number, same still-unconfirmed email, must be allowed to proceed (and clean
        // up the abandoned row rather than pile up two unconfirmed accounts sharing one email).
        await Should.NotThrowAsync(() => WithUnitOfWorkAsync(() => _otpAppService.RegisterAsync(NewRegisterDto(NewPhoneNumber(), email))));
    }
}
