using System;
using Volo.Abp.Application.Dtos;

namespace Eksabli.Engagement;

// Business-facing view of a follower — same cross-realm join shape as Memberships.MemberDto
// (CustomerProfile + IdentityUser, both Host-realm, soft-referenced via CustomerId). FollowDto stays
// bare/unenriched for GetMyFollowsAsync (self-service — a customer doesn't need their own name
// echoed back); this is the "Following" tab's business-facing counterpart, from GetFollowersAsync.
public class FollowerDto : EntityDto<Guid>
{
    public Guid CustomerId { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string? PhoneNumber { get; set; }

    public DateTime FollowedAt { get; set; }
}
