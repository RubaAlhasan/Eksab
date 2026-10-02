using Eksabli.Shared;

namespace Eksabli.Pos;

public class AwardPointsByCustomerIdDto
{
    public decimal? PurchaseAmount { get; set; }

    // Required when PurchaseAmount is given — enforced in PosAppService, not via [Required], since a
    // PerVisit-only award legitimately has neither.
    public Currency? Currency { get; set; }
}
