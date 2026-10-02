using System;
using Volo.Abp.Application.Dtos;
using Eksabli.Shared;

namespace Eksabli.Wallets;

public class PointRuleDto : AuditedEntityDto<Guid>
{
    public Guid? TenantId { get; set; }

    public PointRuleType RuleType { get; set; }

    public decimal PointsPerUnit { get; set; }

    public Currency? Currency { get; set; }
}
