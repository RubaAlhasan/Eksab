using System;

namespace Eksabli.Memberships;

[Serializable]
public class WalletQrCacheItem
{
    // Minted by a Host-realm customer (no tenant) but redeemed by tenant-realm staff (their own
    // tenant) — ABP's typed IDistributedCache<T,TKey> namespaces keys by the *current* tenant, which
    // would put the mint and the read on different keys. Raw Microsoft.Extensions.Caching.Distributed
    // .IDistributedCache is used instead (see MembershipAppService/PosAppService), with this prefix
    // applied manually, so the key is identical regardless of who's calling.
    public const string CacheKeyPrefix = "WalletQr:";

    public Guid CustomerId { get; set; }
}
