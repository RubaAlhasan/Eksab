using System.ComponentModel.DataAnnotations;

namespace Eksabli.Pos;

public class AwardPointsByQrDto
{
    [Required]
    public string QrToken { get; set; } = string.Empty;

    public decimal? PurchaseAmount { get; set; }
}
