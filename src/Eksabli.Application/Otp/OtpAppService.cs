using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Distributed;
using Eksabli.CustomerProfiles;
using Eksabli.Sms;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Caching;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;

namespace Eksabli.Otp;

[RemoteService(IsEnabled = false)]
public class OtpAppService : ApplicationService, IOtpAppService
{
    private readonly IDistributedCache<OtpCacheItem, string> _otpCache;
    private readonly ISmsSender _smsSender;
    private readonly IIdentityUserRepository _identityUserRepository;
    private readonly IdentityUserManager _identityUserManager;
    private readonly IRepository<CustomerProfile, Guid> _customerProfileRepository;
    private readonly ICurrentTenant _currentTenant;

    public OtpAppService(
        IDistributedCache<OtpCacheItem, string> otpCache,
        ISmsSender smsSender,
        IIdentityUserRepository identityUserRepository,
        IdentityUserManager identityUserManager,
        IRepository<CustomerProfile, Guid> customerProfileRepository,
        ICurrentTenant currentTenant)
    {
        _otpCache = otpCache;
        _smsSender = smsSender;
        _identityUserRepository = identityUserRepository;
        _identityUserManager = identityUserManager;
        _customerProfileRepository = customerProfileRepository;
        _currentTenant = currentTenant;
    }

    public async Task RequestOtpAsync(RequestOtpDto input)
    {
        // Normalized here so the cache key matches whatever OtpLoginService.ValidateAndResolveUserAsync
        // normalizes the same phone number to on the verify step — see PhoneNumberNormalizer's own
        // comment. Send the SMS to the number the customer actually typed, not the normalized form.
        var normalizedPhoneNumber = PhoneNumberNormalizer.Normalize(input.PhoneNumber);
        await SendCodeAsync(normalizedPhoneNumber, input.PhoneNumber);
    }

    // Matches prototype/customer/register.html's field set exactly: it submits this form, then goes
    // straight to otp-verify.html — no separate "send code" step in the UI — so this call sends the
    // code itself too, same as RequestOtpAsync. Creates the IdentityUser + CustomerProfile right away
    // (fully named/emailed/etc., not blank), but the account stays PhoneNumberConfirmed = false —
    // unusable until the OTP step actually proves the phone number — see
    // OtpLoginService.ValidateAndResolveUserAsync for where that flips to true.
    public async Task RegisterAsync(RegisterCustomerDto input)
    {
        var normalizedPhoneNumber = PhoneNumberNormalizer.Normalize(input.PhoneNumber);

        using (_currentTenant.Change(null)) // customers are Host-realm, same identity space as OtpLoginService
        {
            var existingUser = await _identityUserRepository.FindByNormalizedUserNameAsync(normalizedPhoneNumber.ToUpperInvariant());
            if (existingUser != null)
            {
                if (existingUser.PhoneNumberConfirmed)
                {
                    // Here the caller already proved they know this exact number owns an account by
                    // trying to register it, so being specific (vs. LookupCustomerByPhoneAsync's
                    // deliberately vague "no matching customer" for staff) is fine.
                    throw new UserFriendlyException("This phone number is already registered. Please log in instead.");
                }

                // An earlier registration for this number was started but never finished (OTP step
                // never completed) — clear it out and start over rather than reject the retry or pile
                // up duplicate unconfirmed accounts per phone number.
                var abandonedProfile = await _customerProfileRepository.FirstOrDefaultAsync(p => p.UserId == existingUser.Id);
                if (abandonedProfile != null)
                {
                    await _customerProfileRepository.DeleteAsync(abandonedProfile);
                }

                (await _identityUserManager.DeleteAsync(existingUser)).CheckErrors();
            }

            var email = input.Email.IsNullOrWhiteSpace() ? $"{Guid.NewGuid():N}@otp.eksabli.local" : input.Email!;
            var user = new IdentityUser(GuidGenerator.Create(), normalizedPhoneNumber, email, tenantId: null);
            user.SetPhoneNumber(normalizedPhoneNumber, confirmed: false);

            (await _identityUserManager.CreateAsync(user)).CheckErrors();
            (await _identityUserManager.AddPasswordAsync(user, input.Password)).CheckErrors();

            var profile = CustomerProfile.Create(GuidGenerator.Create(), user.Id);
            profile.SetName(input.FirstName, input.LastName);
            profile.SetDateOfBirth(input.DateOfBirth);
            if (input.Gender.HasValue)
            {
                profile.SetGender(input.Gender.Value);
            }

            await _customerProfileRepository.InsertAsync(profile, autoSave: true);
        }

        await SendCodeAsync(normalizedPhoneNumber, input.PhoneNumber);
    }

    private async Task SendCodeAsync(string normalizedPhoneNumber, string rawPhoneNumber)
    {
        var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

        await _otpCache.SetAsync(
            normalizedPhoneNumber,
            new OtpCacheItem { Code = code },
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
            });

        await _smsSender.SendAsync(rawPhoneNumber, $"Your Eksabli verification code is {code}");
    }
}
