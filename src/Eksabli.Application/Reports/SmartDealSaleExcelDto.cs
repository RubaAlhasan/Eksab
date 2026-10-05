using System;

namespace Eksabli.Reports;

// One row of the "Smart deal sales" Excel file. Flat, with plain text currency, so the sheet reads the same in any
// spreadsheet app and nothing depends on an enum's number.
public class SmartDealSaleExcelDto
{
    public DateTime? CompletedAt { get; set; }

    public Guid DealId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string OfferTitleEn { get; set; } = string.Empty;

    public string OfferTitleAr { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal BasePrice { get; set; }

    public decimal TotalAmount { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;

    public DateTime ServiceDate { get; set; }

    public string? BranchName { get; set; }

    public string? StaffEmail { get; set; }

    public string CustomerName { get; set; } = string.Empty;
}
