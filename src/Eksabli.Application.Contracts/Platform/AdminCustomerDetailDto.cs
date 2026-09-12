using System;
using System.Collections.Generic;

namespace Eksabli.Platform;

// Admin Portal > Users > Customer Details — the Host-realm profile plus every business membership
// this customer has joined, each with its own independent wallet (see AdminCustomerMembershipDto).
public class AdminCustomerDetailDto
{
    public Guid Id { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string? Contact { get; set; }

    public bool IsActive { get; set; }

    // Platform join date (CustomerProfile.CreationTime) — not any one business's JoinedAt.
    public DateTime CreationTime { get; set; }

    public List<AdminCustomerMembershipDto> Memberships { get; set; } = new();
}
