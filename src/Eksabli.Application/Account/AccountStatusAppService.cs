using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Distributed;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Caching;
using Volo.Abp.Identity;
using Volo.Abp.Users;

namespace Eksabli.Account;

// Small, standalone concept rather than folded into EmployeeAssignmentAppService —
// IdentityUser.ShouldChangePasswordOnNextLogin is a general framework property, not specific to invited
// staff (EmployeeAssignmentAppService.InviteAsync is just the one place in this codebase that currently
// sets it), so this reads it generically for whoever is currently authenticated rather than assuming
// "employee". Self-service only — always reads the CALLER's own flag, never takes a target user id.
//
// Cached 30s per user (same TTL convention as the Excel-download tokens elsewhere in this app) because
// MustChangePasswordFilter (Eksabli.HttpApi.Host) calls this on every authenticated API request to
// enforce the flag server-side, not just via the Angular route guard — without caching that would be a
// full IdentityUser load on every single request.
[RemoteService(IsEnabled = false)]
public class AccountStatusAppService : ApplicationService, IAccountStatusAppService
{
    private readonly IIdentityUserRepository _identityUserRepository;
    private readonly IDistributedCache<MustChangePasswordCacheItem, Guid> _cache;

    public AccountStatusAppService(
        IIdentityUserRepository identityUserRepository,
        IDistributedCache<MustChangePasswordCacheItem, Guid> cache)
    {
        _identityUserRepository = identityUserRepository;
        _cache = cache;
    }

    public async Task<bool> GetMustChangePasswordAsync()
    {
        var userId = CurrentUser.GetId();

        var cacheItem = await _cache.GetOrAddAsync(
            userId,
            async () =>
            {
                var user = await _identityUserRepository.GetAsync(userId);
                return new MustChangePasswordCacheItem { MustChangePassword = user.ShouldChangePasswordOnNextLogin };
            },
            () => new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) });

        return cacheItem!.MustChangePassword;
    }
}
