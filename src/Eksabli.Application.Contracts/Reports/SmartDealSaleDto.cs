using System;
using Eksabli.Shared;
using Volo.Abp.Application.Dtos;

namespace Eksabli.Reports;

// One completed Buy Now sale on the Business Portal > Transactions "Smart deal sales" tab. Deliberately not a
// TransactionListItemDto: a sale is money in the deal's own currency, not points, and it has no ledger rows to group.
// The offer title is the snapshot taken when the order was placed, so an edited or deleted deal still reads correctly.
public class SmartDealSaleDto : EntityDto<Guid>
{
    // Lets the sale details link to the deal itself.
    public Guid SmartOfferId { get; set; }

    public string Code { get; set; } = string.Empty;

    // The deal's description, read live from the offer. Null when the deal has been deleted.
    public string? OfferDescriptionAr { get; set; }

    public string? OfferDescriptionEn { get; set; }

    public string OfferTitleAr { get; set; } = string.Empty;

    public string OfferTitleEn { get; set; } = string.Empty;

    public int Quantity { get; set; }

    // The price the customer was quoted, and the regular price, so a detail view can show the saving.
    public decimal UnitPrice { get; set; }

    public decimal BasePrice { get; set; }

    public decimal TotalAmount { get; set; }

    public Currency Currency { get; set; }

    public DateOnly ServiceDate { get; set; }

    public DateTime PlacedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public Guid? CompletedBranchId { get; set; }

    public string? BranchName { get; set; }

    public Guid? CompletedByEmployeeId { get; set; }

    public string? StaffEmail { get; set; }

    public string? CustomerFirstName { get; set; }

    public string? CustomerLastName { get; set; }
}
