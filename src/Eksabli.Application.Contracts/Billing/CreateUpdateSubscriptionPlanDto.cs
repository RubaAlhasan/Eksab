using System.ComponentModel.DataAnnotations;

namespace Eksabli.Billing;

public class CreateUpdateSubscriptionPlanDto
{
    [Required]
    [StringLength(SubscriptionPlanConsts.MaxNameLength)]
    public string Name { get; set; } = string.Empty;

    [Range(typeof(decimal), "0", "79228162514264337593543950335")]
    public decimal MonthlyPrice { get; set; }

    [StringLength(SubscriptionPlanConsts.MaxFeatureLimitsJsonLength)]
    public string FeatureLimitsJson { get; set; } = "{}";

    public bool IsTrialDefault { get; set; }
}
