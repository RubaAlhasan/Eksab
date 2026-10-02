using System.ComponentModel.DataAnnotations;
using Eksabli.Shared;

namespace Eksabli.Pos;

public class AwardPointsByQrDto
{
    [Required]
    public string QrToken { get; set; } = string.Empty;

    public decimal? PurchaseAmount { get; set; }

    // Required when PurchaseAmount is given — see AwardPointsByCustomerIdDto.Currency's own comment.
    public Currency? Currency { get; set; }
}
