using System;
using System.ComponentModel.DataAnnotations;

namespace Eksabli.Billing;

public class ChangePlanDto
{
    [Required]
    public Guid PlanId { get; set; }
}
