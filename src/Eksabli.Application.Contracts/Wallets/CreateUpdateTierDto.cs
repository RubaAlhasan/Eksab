using System.ComponentModel.DataAnnotations;

namespace Eksabli.Wallets;

public class CreateUpdateTierDto
{
    [Required]
    [StringLength(TierConsts.MaxNameLength)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public int MinLifetimePoints { get; set; }

    [Required]
    public decimal Multiplier { get; set; }
}
