using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace Eksabli.Wallets;

public class PointRule : AuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }

    public PointRuleType RuleType { get; private set; }

    public decimal PointsPerUnit { get; private set; }

    protected PointRule()
    {
        /* Required by the ORM */
    }

    private PointRule(Guid id, PointRuleType ruleType, decimal pointsPerUnit)
        : base(id)
    {
        RuleType = ruleType;
        PointsPerUnit = pointsPerUnit;
    }

    public static PointRule Create(Guid id, PointRuleType ruleType, decimal pointsPerUnit)
    {
        return new PointRule(id, ruleType, pointsPerUnit);
    }

    public void SetPointsPerUnit(decimal pointsPerUnit) => PointsPerUnit = pointsPerUnit;
}
