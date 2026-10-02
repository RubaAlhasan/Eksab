using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;
using Eksabli.Shared;

namespace Eksabli.Wallets;

public class PointRule : AuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }

    public PointRuleType RuleType { get; private set; }

    public decimal PointsPerUnit { get; private set; }

    // Which currency this rate is denominated in — required for PerCurrencyUnit (a rate like "1 point
    // per unit" is meaningless without knowing which currency the unit is), and always null for
    // PerVisit (a flat points-per-visit reward isn't pegged to any money amount at all). Immutable
    // after create, same as RuleType — see PointRuleAppService.UpdateAsync's own comment.
    public Currency? Currency { get; private set; }

    protected PointRule()
    {
        /* Required by the ORM */
    }

    private PointRule(Guid id, PointRuleType ruleType, decimal pointsPerUnit, Currency? currency)
        : base(id)
    {
        if (ruleType == PointRuleType.PerCurrencyUnit && currency == null)
        {
            throw new ArgumentException("A currency is required for a per-currency-unit point rule.", nameof(currency));
        }

        if (ruleType == PointRuleType.PerVisit && currency != null)
        {
            throw new ArgumentException("A per-visit point rule has no currency.", nameof(currency));
        }

        RuleType = ruleType;
        PointsPerUnit = pointsPerUnit;
        Currency = currency;
    }

    public static PointRule Create(Guid id, PointRuleType ruleType, decimal pointsPerUnit, Currency? currency = null)
    {
        return new PointRule(id, ruleType, pointsPerUnit, currency);
    }

    public void SetPointsPerUnit(decimal pointsPerUnit) => PointsPerUnit = pointsPerUnit;
}
