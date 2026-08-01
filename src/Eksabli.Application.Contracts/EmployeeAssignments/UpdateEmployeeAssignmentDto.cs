using System;
using System.ComponentModel.DataAnnotations;

namespace Eksabli.EmployeeAssignments;

public class UpdateEmployeeAssignmentDto
{
    [Required]
    public EmployeeRole Role { get; set; }

    public Guid? BranchId { get; set; }
}
