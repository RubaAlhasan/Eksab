using System;
using System.ComponentModel.DataAnnotations;

namespace Eksabli.EmployeeAssignments;

public class InviteEmployeeDto
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public EmployeeRole Role { get; set; }

    public Guid? BranchId { get; set; }
}
