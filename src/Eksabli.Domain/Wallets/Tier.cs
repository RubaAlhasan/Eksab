using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace Eksabli.Wallets;

public class Tier : AuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }

    public string Name { get; private set; }

    public int MinLifetimePoints { get; private set; }

    public decimal Multiplier { get; private set; }

    protected Tier()
    {
        Name = string.Empty;
    }

    private Tier(Guid id, string name, int minLifetimePoints, decimal multiplier)
        : base(id)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), TierConsts.MaxNameLength);
        MinLifetimePoints = minLifetimePoints;
        Multiplier = multiplier;
    }

    public static Tier Create(Guid id, string name, int minLifetimePoints, decimal multiplier)
    {
        return new Tier(id, name, minLifetimePoints, multiplier);
    }

    public void SetName(string name) => Name = Check.NotNullOrWhiteSpace(name, nameof(name), TierConsts.MaxNameLength);

    public void SetMinLifetimePoints(int minLifetimePoints) => MinLifetimePoints = minLifetimePoints;

    public void SetMultiplier(decimal multiplier) => Multiplier = multiplier;
}
