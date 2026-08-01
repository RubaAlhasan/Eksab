using System;

namespace Eksabli.Pos;

public class CustomerLookupResultDto
{
    public Guid CustomerId { get; set; }

    public Guid MembershipId { get; set; }

    public Guid WalletId { get; set; }

    public int Balance { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }
}
