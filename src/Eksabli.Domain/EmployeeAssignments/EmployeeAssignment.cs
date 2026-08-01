using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace Eksabli.EmployeeAssignments;

public class EmployeeAssignment : AuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }

    // Soft reference — tenant-realm IdentityUser.Id (no EF FK, same convention as Membership.CustomerId).
    public Guid UserId { get; private set; }

    // null = access to all branches.
    public Guid? BranchId { get; private set; }

    public EmployeeRole Role { get; private set; }

    protected EmployeeAssignment()
    {
        /* Required by the ORM */
    }

    private EmployeeAssignment(Guid id, Guid userId, EmployeeRole role, Guid? branchId)
        : base(id)
    {
        UserId = userId;
        Role = role;
        BranchId = branchId;
    }

    public static EmployeeAssignment Create(Guid id, Guid userId, EmployeeRole role, Guid? branchId = null)
    {
        return new EmployeeAssignment(id, userId, role, branchId);
    }

    public void ChangeRole(EmployeeRole role) => Role = role;

    public void ReassignBranch(Guid? branchId) => BranchId = branchId;
}
