using System;
using System.Collections.Generic;
using System.Linq;
using Eksabli.Shared;

namespace Eksabli.SmartOffers.Pricing;

// One price for the whole local service day. Quantity resets each local day (DailyQuantity), and the slot is the
// offer itself, so every day's stock is a separate inventory row.
internal sealed class FixedPricingStrategy : ISmartPricingStrategy
{
    public SmartPriceQuote? Quote(SmartOffer offer, DateTime nowUtc)
    {
        if (!offer.IsEnabled)
        {
            return null;
        }

        var timeZone = SmartOfferTiming.ResolveTimeZone(offer.TimeZoneId);
        var (today, _) = SmartOfferTiming.ToLocal(nowUtc, timeZone);
        if (!offer.IsWithinValidity(today))
        {
            return null;
        }

        return new SmartPriceQuote(
            SlotId: offer.Id,
            StageId: null,
            Price: offer.BasePrice,
            BasePrice: offer.BasePrice,
            Currency: offer.Currency,
            WindowStartUtc: SmartOfferTiming.ToUtc(today, 0, timeZone),
            WindowEndUtc: SmartOfferTiming.ToUtc(today, SmartOfferConsts.MinutesPerDay, timeZone),
            QuantityLimit: offer.DailyQuantity);
    }

    public IEnumerable<DateTime> ChangePointsUtc(SmartOffer offer, DateTime afterUtc)
    {
        // The price itself never changes across midnight. The only real change is validity starting or ending,
        // which midnight boundaries cover once the search reaches the day that matters.
        var timeZone = SmartOfferTiming.ResolveTimeZone(offer.TimeZoneId);
        var (today, _) = SmartOfferTiming.ToLocal(afterUtc, timeZone);

        return Enumerable.Range(0, SmartOfferConsts.NextChangeLookaheadDays + 1)
            .Select(offset => SmartOfferTiming.ToUtc(today.AddDays(offset), 0, timeZone))
            .Where(instant => instant > afterUtc)
            .Distinct()
            .OrderBy(instant => instant);
    }
}

// Price follows the day's stages. A stage is in force for [StartMinute, EndMinute), so a stage that ends at 11:00
// and the next that starts at 11:00 hand over cleanly. A moment between stages has no price (not on sale).
internal sealed class TimeBasedPricingStrategy : ISmartPricingStrategy
{
    public SmartPriceQuote? Quote(SmartOffer offer, DateTime nowUtc)
    {
        if (!offer.IsEnabled)
        {
            return null;
        }

        var timeZone = SmartOfferTiming.ResolveTimeZone(offer.TimeZoneId);
        var (today, minute) = SmartOfferTiming.ToLocal(nowUtc, timeZone);
        if (!offer.IsWithinValidity(today))
        {
            return null;
        }

        var stage = offer.Stages.FirstOrDefault(s => s.StartMinute <= minute && minute < s.EndMinute);
        if (stage == null)
        {
            return null;
        }

        return new SmartPriceQuote(
            SlotId: stage.Id,
            StageId: stage.Id,
            Price: stage.Price,
            BasePrice: offer.BasePrice,
            Currency: stage.Currency,
            WindowStartUtc: SmartOfferTiming.ToUtc(today, stage.StartMinute, timeZone),
            WindowEndUtc: SmartOfferTiming.ToUtc(today, stage.EndMinute, timeZone),
            QuantityLimit: stage.QuantityLimit);
    }

    public IEnumerable<DateTime> ChangePointsUtc(SmartOffer offer, DateTime afterUtc)
    {
        var timeZone = SmartOfferTiming.ResolveTimeZone(offer.TimeZoneId);
        var (today, _) = SmartOfferTiming.ToLocal(afterUtc, timeZone);

        return Enumerable.Range(0, SmartOfferConsts.NextChangeLookaheadDays)
            .SelectMany(offset =>
            {
                var date = today.AddDays(offset);
                return offer.Stages.SelectMany(stage => new[]
                {
                    SmartOfferTiming.ToUtc(date, stage.StartMinute, timeZone),
                    SmartOfferTiming.ToUtc(date, stage.EndMinute, timeZone),
                });
            })
            .Where(instant => instant > afterUtc)
            .Distinct()
            .OrderBy(instant => instant);
    }
}
