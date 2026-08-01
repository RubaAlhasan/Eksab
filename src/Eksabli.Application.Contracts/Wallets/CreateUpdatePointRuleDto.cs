using System.ComponentModel.DataAnnotations;

namespace Eksabli.Wallets;

public class CreateUpdatePointRuleDto
{
    [Required]
    public PointRuleType RuleType { get; set; }

    [Required]
    public decimal PointsPerUnit { get; set; }
}
