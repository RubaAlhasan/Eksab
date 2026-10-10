using System;
using System.Collections.Generic;
using Eksabli.Shared;

namespace Eksabli.SmartOffers;

// Input shapes for SmartOffer.Create / Update. Records, not DTOs: the domain validates these itself, so a
// caller that skips the application layer still cannot persist an inconsistent offer.

public sealed record SmartOfferDetails(
    string TitleAr,
    string TitleEn,
    string? DescriptionAr,
    string? DescriptionEn);

// Stage Id is supplied by the caller, not generated here. A new stage gets a fresh id from the application
// layer's GuidGenerator, and an existing stage keeps the id it already has. That's what lets a pending order
// keep pointing at a stable slot even while the owner edits the stage's price or quantity.
public sealed record SmartOfferStageSpec(
    Guid Id,
    int StartMinute,
    int EndMinute,
    decimal Price,
    Currency Currency,
    int? QuantityLimit);

public sealed record SmartOfferPricingSpec(
    SmartPricingStrategy Strategy,
    Currency Currency,
    decimal BasePrice,
    decimal? MinimumPrice,
    int? DailyQuantity,
    IReadOnlyList<SmartOfferStageSpec> Stages);

public sealed record SmartOfferScheduleSpec(
    string TimeZoneId,
    DateOnly? ValidFrom,
    DateOnly? ValidTo);

// The quote a customer is shown and that an order snapshots. Everything the order needs to be honored later
// is here, so confirmation never has to re-derive the price from the offer's current configuration.
public sealed record SmartPriceQuote(
    Guid SlotId,
    Guid? StageId,
    decimal Price,
    decimal BasePrice,
    Currency Currency,
    DateTime WindowStartUtc,
    DateTime WindowEndUtc,
    int? QuantityLimit);

// Next moment the effective price changes. Price is null when the change is to "not on sale".
public sealed record SmartPriceChange(DateTime AtUtc, decimal? Price);
