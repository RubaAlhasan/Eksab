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
        var cached = await _otpCache.GetAsync(phoneNumber);
        if (cached == null)
        {
            return new OtpValidationResult { IsValid = false, ErrorCode = "expired_code" };
        }

        if (cached.Code != code)
        {
            // Do NOT remove the cache entry here — a wrong guess must not burn the real code.
            return new OtpValidationResult { IsValid = false, ErrorCode = "invalid_code" };
        }

        await _otpCache.RemoveAsync(phoneNumber); // single-use — burn only on a successful match

        using (_currentTenant.Change(null)) // customers are Host-realm, same identity space as Membership.CustomerId
        {
            var user = await _identityUserRepository.FindByNormalizedUserNameAsync(phoneNumber.ToUpperInvariant());
            var isNew = false;

            if (user == null)
            {
                user = new IdentityUser(_guidGenerator.Create(), phoneNumber, $"{Guid.NewGuid():N}@otp.eksabli.local", tenantId: null);
                (await _identityUserManager.CreateAsync(user)).CheckErrors();
                await _identityUserManager.SetPhoneNumberAsync(user, phoneNumber);

                var profile = CustomerProfile.Create(_guidGenerator.Create(), user.Id);
                await _customerProfileRepository.InsertAsync(profile, autoSave: true);

                isNew = true;
            }

            return new OtpValidationResult { IsValid = true, User = user, IsNewUser = isNew };
        }
    }
}
