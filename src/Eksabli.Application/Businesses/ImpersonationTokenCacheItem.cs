using System;

namespace Eksabli.Businesses;

[Serializable]
public class ImpersonationTokenCacheItem
{
    // Minted by a Host-realm admin, redeemed by TenantImpersonationGrantHandler from inside the
    // OpenIddict token endpoint pipeline (no ambient ICurrentTenant scoping there) — raw
    // Microsoft.Extensions.Caching.Distributed.IDistributedCache is used instead of the typed
    // IDistributedCache<T,TKey>, same reasoning as WalletQrCacheItem's own comment, with this prefix
    // applied manually so the key doesn't depend on tenant context at either end.
    public const string CacheKeyPrefix = "TenantImpersonation:";

    public Guid TenantId { get; set; }
}
