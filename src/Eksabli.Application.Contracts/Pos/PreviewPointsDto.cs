using Eksabli.Shared;

namespace Eksabli.Pos;

public class PreviewPointsDto
{
    public decimal? PurchaseAmount { get; set; }

    // Required when PurchaseAmount is given — see AwardPointsByCustomerIdDto.Currency's own comment.
    public Currency? Currency { get; set; }
}
