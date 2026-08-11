using System;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Eksabli.CustomerProfiles;
using Eksabli.Sms;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Caching;

namespace Eksabli.Otp;

[RemoteService(IsEnabled = false)]
public class OtpAppService : ApplicationService, IOtpAppService
{
    private readonly IDistributedCache<OtpCacheItem, string> _otpCache;
    private readonly ISmsSender _smsSender;

    public OtpAppService(IDistributedCache<OtpCacheItem, string> otpCache, ISmsSender smsSender)
    {
        _otpCache = otpCache;
        _smsSender = smsSender;
    }

    public async Task RequestOtpAsync(RequestOtpDto input)
    {
        var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();

        // Normalized here so the cache key matches whatever OtpLoginService.ValidateAndResolveUserAsync
        // normalizes the same phone number to on the verify step — see PhoneNumberNormalizer's own
        // comment. Send the SMS to the number the customer actually typed, not the normalized form.
        var normalizedPhoneNumber = PhoneNumberNormalizer.Normalize(input.PhoneNumber);

        await _otpCache.SetAsync(
            normalizedPhoneNumber,
            new OtpCacheItem { Code = code },
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
            });

        await _smsSender.SendAsync(input.PhoneNumber, $"Your Eksabli verification code is {code}");
    }
}
