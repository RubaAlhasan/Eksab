using System;

namespace Eksabli.Businesses;

public class BusinessRegistrationResultDto
{
    public Guid TenantId { get; set; }

    public string TenantName { get; set; } = string.Empty;

    public Guid BusinessProfileId { get; set; }

    public Guid BranchId { get; set; }

    public Guid OwnerUserId { get; set; }
}
