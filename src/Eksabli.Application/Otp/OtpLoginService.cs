using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Eksabli.CustomerProfiles;
using Volo.Abp.Caching;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Guids;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;

namespace Eksabli.Otp;

public class OtpLoginService : IOtpLoginService, ITransientDependency
{
    private readonly IDistributedCache<OtpCacheItem, string> _otpCache;
    private readonly IdentityUserManager _identityUserManager;
    private readonly IIdentityUserRepository _identityUserRepository;
    private readonly IRepository<CustomerProfile, Guid> _customerProfileRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IGuidGenerator _guidGenerator;

    public OtpLoginService(
        IDistributedCache<OtpCacheItem, string> otpCache,
        IdentityUserManager identityUserManager,
        IIdentityUserRepository identityUserRepository,
        IRepository<CustomerProfile, Guid> customerProfileRepository,
        ICurrentTenant currentTenant,
        IGuidGenerator guidGenerator)
    {
        _otpCache = otpCache;
        _identityUserManager = identityUserManager;
        _identityUserRepository = identityUserRepository;
        _customerProfileRepository = customerProfileRepository;
        _currentTenant = currentTenant;
        _guidGenerator = guidGenerator;
    }

    public async Task<OtpValidationResult> ValidateAndResolveUserAsync(string phoneNumber, string code)
    {
        // Normalized once, used for the cache key, the lookup, AND (for a brand-new customer) the
        // IdentityUser.UserName itself — see PhoneNumberNormalizer's own comment for why this needs to
        // be the exact same value PosAppService.LookupCustomerByPhoneAsync normalizes to later, not
        // just internally consistent within this one method.
        var normalizedPhoneNumber = PhoneNumberNormalizer.Normalize(phoneNumber);

        var cached = await _otpCache.GetAsync(normalizedPhoneNumber);
        if (cached == null)
        {
            return new OtpValidationResult { IsValid = false, ErrorCode = "expired_code" };
        }

        if (cached.Code != code)
        {
            // Do NOT remove the cache entry here — a wrong guess must not burn the real code.
            return new OtpValidationResult { IsValid = false, ErrorCode = "invalid_code" };
        }

        await _otpCache.RemoveAsync(normalizedPhoneNumber); // single-use — burn only on a successful match

        using (_currentTenant.Change(null)) // customers are Host-realm, same identity space as Membership.CustomerId
        {
            var user = await _identityUserRepository.FindByNormalizedUserNameAsync(normalizedPhoneNumber.ToUpperInvariant());
            var isNew = false;

            if (user == null)
            {
                // No prior OtpAppService.RegisterAsync call for this number at all — e.g. a business
                // added this customer directly via POS, or the app's "log in with OTP" was used for a
                // number that was never formally registered. Keep the old auto-create-blank-profile
                // fallback so OTP login still works standalone, without forcing registration first.
                user = new IdentityUser(_guidGenerator.Create(), normalizedPhoneNumber, $"{Guid.NewGuid():N}@otp.eksabli.local", tenantId: null);
                (await _identityUserManager.CreateAsync(user)).CheckErrors();
                await _identityUserManager.SetPhoneNumberAsync(user, normalizedPhoneNumber);

                // SetPhoneNumberAsync alone leaves PhoneNumberConfirmed false (Identity's default
                // behavior on any phone number change) — without this, EVERY subsequent login for this
                // same user would fall into the "else if (!user.PhoneNumberConfirmed)" branch below and
                // keep reporting IsNewUser = true, not just the first one. A correct OTP match here IS
                // the confirmation proof, same as the registered-user path below.
                user.SetPhoneNumberConfirmed(true);
                (await _identityUserManager.UpdateAsync(user)).CheckErrors();

                var profile = CustomerProfile.Create(_guidGenerator.Create(), user.Id);
                await _customerProfileRepository.InsertAsync(profile, autoSave: true);

                isNew = true;
            }
            else if (!user.PhoneNumberConfirmed)
            {
                // Completes a registration started via OtpAppService.RegisterAsync — the IdentityUser
                // and its CustomerProfile (name/email/dob/gender) already exist, they just needed this
                // phone number proven. First real, usable login for this account.
                user.SetPhoneNumberConfirmed(true);
                (await _identityUserManager.UpdateAsync(user)).CheckErrors();

                isNew = true;
            }

            return new OtpValidationResult { IsValid = true, User = user, IsNewUser = isNew };
        }
    }
}
