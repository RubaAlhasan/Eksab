using System;
using Volo.Abp.Application.Dtos;

namespace Eksabli.EmployeeAssignments;

public class EmployeeAssignmentDto : AuditedEntityDto<Guid>
{
    public Guid UserId { get; set; }

    public string? UserEmail { get; set; }

    public Guid? BranchId { get; set; }

    public EmployeeRole Role { get; set; }
}
