using System;
using System.Threading.Tasks;
using Volo.Abp.Caching;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities.Events;
using Volo.Abp.EventBus;
using Volo.Abp.Identity;

namespace Eksabli.Account;

// Invalidates the must-change-password cache the moment any IdentityUser row changes — most importantly,
// the instant ABP's own ProfileAppService.ChangePasswordAsync clears ShouldChangePasswordOnNextLogin after
// a successful password change, so MustChangePasswordFilter (Eksabli.HttpApi.Host) doesn't keep blocking
// the user against a stale cached "true" for the rest of AccountStatusAppService's 30s TTL. A blanket
// removal on every IdentityUser change is simpler and safer than trying to diff the flag's old/new value —
// worst case it's one extra DB read on the next request, not a correctness issue.
public class MustChangePasswordCacheInvalidator :
    ILocalEventHandler<EntityChangedEventData<IdentityUser>>,
    ITransientDependency
{
    private readonly IDistributedCache<MustChangePasswordCacheItem, Guid> _cache;

    public MustChangePasswordCacheInvalidator(IDistributedCache<MustChangePasswordCacheItem, Guid> cache)
    {
        _cache = cache;
    }

    public async Task HandleEventAsync(EntityChangedEventData<IdentityUser> eventData)
    {
        await _cache.RemoveAsync(eventData.Entity.Id);
    }
}
