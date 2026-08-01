using System;
using Volo.Abp.Application.Dtos;

namespace Eksabli.Wallets;

public class TierDto : AuditedEntityDto<Guid>
{
    public Guid? TenantId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int MinLifetimePoints { get; set; }

    public decimal Multiplier { get; set; }
}
