using System.ComponentModel.DataAnnotations;
using Eksabli.Shared;

namespace Eksabli.Wallets;

public class CreateUpdatePointRuleDto
{
    [Required]
    public PointRuleType RuleType { get; set; }

    [Required]
    public decimal PointsPerUnit { get; set; }

    // Required for PerCurrencyUnit, forbidden for PerVisit — enforced in PointRuleAppService.CreateAsync,
    // not via [Required] here, since the requirement is conditional on RuleType.
    public Currency? Currency { get; set; }
}
