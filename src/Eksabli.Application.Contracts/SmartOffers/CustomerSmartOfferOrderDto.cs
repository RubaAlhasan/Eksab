using System;
using Eksabli.Shared;
using Volo.Abp.Application.Dtos;

namespace Eksabli.SmartOffers;

// A customer's own order, with the business it belongs to, so a history across several businesses reads correctly.
public class CustomerSmartOfferOrderDto : SmartOfferOrderDto
{
    public Guid? TenantId { get; set; }

    public string? BusinessName { get; set; }

    // The deal's description as the business wrote it. Read live from the offer, so an order still shows what the deal
    // was about after its sale window has closed and the deal is no longer on the browse list.
    public string? OfferDescriptionAr { get; set; }

    public string? OfferDescriptionEn { get; set; }
}

// What a customer sees when they open a deal from one of their orders. Works after the deal's sale window has closed.
public class CustomerSmartOfferDetailsDto : EntityDto<Guid>
{
    public Guid TenantId { get; set; }

    public string BusinessName { get; set; } = string.Empty;

    public string TitleAr { get; set; } = string.Empty;

    public string TitleEn { get; set; } = string.Empty;

    public string? DescriptionAr { get; set; }

    public string? DescriptionEn { get; set; }

    public Currency Currency { get; set; }

    public decimal BasePrice { get; set; }
}

public class GetMySmartOfferOrdersInput : PagedAndSortedResultRequestDto
{
    public CustomerDealOrderFilter Filter { get; set; } = CustomerDealOrderFilter.All;
}
