using System;
using Eksabli.BusinessProfiles;

namespace Eksabli.Businesses;

public class AdminTenantDto
{
    public Guid TenantId { get; set; }

    public string TenantName { get; set; } = string.Empty;

    public Guid BusinessProfileId { get; set; }

    public Guid? CategoryId { get; set; }

    public TenantApprovalStatus ApprovalStatus { get; set; }

    public DateTime CreationTime { get; set; }
}
