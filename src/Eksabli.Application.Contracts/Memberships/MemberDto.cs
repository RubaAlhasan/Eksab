using System;
using Volo.Abp.Application.Dtos;

namespace Eksabli.Memberships;

// The business-facing view of one of THIS tenant's members — a join across Membership (this tenant),
// CustomerProfile + IdentityUser (both Host-realm, soft-referenced via CustomerId, same convention
// PosAppService.LookupCustomerByPhoneAsync already uses), and PointsWallet/Tier (this tenant). See
// MembershipAppService.GetMembersAsync for how it's assembled — nothing here is fabricated, every
// field maps to a real column; there is no separate "last active"/"login" tracking anywhere in this
// codebase, so LastActiveAt is PointsWallet.LastModificationTime (bumped by ABP's own auditing
// whenever a real points transaction changes the wallet) — the closest genuine signal, not invented.
public class MemberDto : EntityDto<Guid>
{
    public Guid CustomerId { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string? PhoneNumber { get; set; }

    public DateTime JoinedAt { get; set; }

    public MembershipStatus Status { get; set; }

    public int Balance { get; set; }

    public Guid? TierId { get; set; }

    public string? TierName { get; set; }

    public DateTime? LastActiveAt { get; set; }
}
