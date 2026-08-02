using System;

namespace Eksabli.Reports;

public class TopCustomerDto
{
    public Guid MembershipId { get; set; }

    public Guid CustomerId { get; set; }

    // Proxy for "lifetime value" — no monetary revenue is tracked on PointsTransaction today, only points.
    public int LifetimeEarned { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }
}
