using System;
using System.Collections.Generic;

namespace Eksabli.Businesses;

// Batch tenant-id -> business resolution. Wallet, coupons, memberships and follows
// all return bare TenantIds, so the app would otherwise need one request per row to
// render a name.
public class CustomerBusinessLookupDto
{
    public List<Guid> TenantIds { get; set; } = new();
}
