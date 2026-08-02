using System;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;

namespace Eksabli.Engagement;

// Badge definition — platform-wide (TenantId == null) or tenant-specific. Deliberately NOT
// IMultiTenant: the standard multi-tenant filter would hide platform-wide (null-tenant) rows from any
// tenant-scoped query, which is the opposite of what "platform-wide" means here. Repositories filter
// explicitly instead (TenantId == null || TenantId == current).
public class Achievement : FullAuditedAggregateRoot<Guid>
{
    public Guid? TenantId { get; private set; }

    public string Name { get; private set; }

    // Badge-earning rule, evaluated manually by staff for now — no automatic criteria engine yet (see
    // docs/eksabli-loyalty-platform/features/06-engagement-gamification/README.md).
    public string? CriteriaJson { get; private set; }

    protected Achievement()
    {
        Name = string.Empty;
    }

    private Achievement(Guid id, Guid? tenantId, string name)
        : base(id)
    {
        TenantId = tenantId;
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), AchievementConsts.MaxNameLength);
    }

    public static Achievement Create(Guid id, Guid? tenantId, string name)
    {
        return new Achievement(id, tenantId, name);
    }

    public void SetName(string name)
    {
        Name = Check.NotNullOrWhiteSpace(name, nameof(name), AchievementConsts.MaxNameLength);
    }

    public void SetCriteria(string? criteriaJson) => CriteriaJson = criteriaJson;
}
