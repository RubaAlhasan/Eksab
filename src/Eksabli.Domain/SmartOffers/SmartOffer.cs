using System;
using System.Collections.Generic;
using System.Linq;
using Eksabli.Shared;
using Eksabli.SmartOffers.Pricing;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace Eksabli.SmartOffers;

// A restaurant deal whose price depends on the time of day (or is simply fixed). Distinct from the older
// Offers.Offer, which is an unpriced promotional banner. A SmartOffer is something a customer can actually buy.
//
// Every mutation goes through Create/Update, which validate the whole configuration before touching state.
// Nothing here can leave a half-applied offer behind, and the stage collection is only ever changed through
// that same validated path.
public class SmartOffer : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; private set; }

    public string TitleAr { get; private set; }

    public string TitleEn { get; private set; }

    public string? DescriptionAr { get; private set; }

    public string? DescriptionEn { get; private set; }

    public SmartPricingStrategy Strategy { get; private set; }

    public Currency Currency { get; private set; }

    // The reference price. For a Fixed offer it IS the price. For a TimeBased one it is the "before" price shown
    // struck through, and it caps every stage's price.
    public decimal BasePrice { get; private set; }

    // Floor for any price this offer can ever quote. Null means no floor beyond "greater than zero".
    public decimal? MinimumPrice { get; private set; }

    // Fixed strategy only: units available each local day. Null means unlimited.
    public int? DailyQuantity { get; private set; }

    public string TimeZoneId { get; private set; }

    // Inclusive local dates. Null means open-ended on that side.
    public DateOnly? ValidFrom { get; private set; }

    public DateOnly? ValidTo { get; private set; }

    // Owner's on/off switch. Independent of validity dates, so an owner can pause a live deal without losing its
    // configuration.
    public bool IsEnabled { get; private set; }

    private readonly List<SmartOfferPriceStage> _stages = new();

    public IReadOnlyCollection<SmartOfferPriceStage> Stages => _stages;

    protected SmartOffer()
    {
        TitleAr = string.Empty;
        TitleEn = string.Empty;
        TimeZoneId = string.Empty;
    }

    private SmartOffer(Guid id)
        : base(id)
    {
        TitleAr = string.Empty;
        TitleEn = string.Empty;
        TimeZoneId = string.Empty;
    }

    public static SmartOffer Create(
        Guid id,
        SmartOfferDetails details,
        SmartOfferPricingSpec pricing,
        SmartOfferScheduleSpec schedule,
        bool isEnabled)
    {
        var offer = new SmartOffer(id);
        offer.Apply(details, pricing, schedule, isEnabled);
        return offer;
    }

    public void Update(
        SmartOfferDetails details,
        SmartOfferPricingSpec pricing,
        SmartOfferScheduleSpec schedule,
        bool isEnabled)
    {
        Apply(details, pricing, schedule, isEnabled);
    }

    public void SetEnabled(bool isEnabled) => IsEnabled = isEnabled;

    // The price in force at nowUtc, or null when nothing is on sale right now.
    public SmartPriceQuote? Quote(DateTime nowUtc) => SmartPricingStrategies.For(Strategy).Quote(this, nowUtc);

    // Where the owner is in the offer's lifecycle at nowUtc. Derived, never stored.
    public SmartOfferStatus GetStatus(DateTime nowUtc)
    {
        if (!IsEnabled)
        {
            return SmartOfferStatus.Paused;
        }

        var (today, _) = SmartOfferTiming.ToLocal(nowUtc, ResolveTimeZone());
        if (ValidTo.HasValue && today > ValidTo.Value)
        {
            return SmartOfferStatus.Expired;
        }

        if (ValidFrom.HasValue && today < ValidFrom.Value)
        {
            return SmartOfferStatus.Scheduled;
        }

        return Quote(nowUtc) != null ? SmartOfferStatus.Live : SmartOfferStatus.BetweenStages;
    }

    // The next time the effective price (or the slot selling it) changes, and what it changes to. Null when no
    // change is in sight within the lookahead, which for an unscheduled-but-enabled offer just means "no change
    // to announce".
    public SmartPriceChange? GetNextChange(DateTime nowUtc)
    {
        var strategy = SmartPricingStrategies.For(Strategy);
        var current = Quote(nowUtc);

        foreach (var instant in strategy.ChangePointsUtc(this, nowUtc))
        {
            var candidate = Quote(instant);
            if (!IsSameOffer(current, candidate))
            {
                return new SmartPriceChange(instant, candidate?.Price);
            }
        }

        return null;
    }

    // Whether a local calendar date falls inside the offer's validity. Both bounds are inclusive.
    public bool IsWithinValidity(DateOnly localDate) =>
        (!ValidFrom.HasValue || localDate >= ValidFrom.Value) &&
        (!ValidTo.HasValue || localDate <= ValidTo.Value);

    public TimeZoneInfo ResolveTimeZone() => SmartOfferTiming.ResolveTimeZone(TimeZoneId);

    // The local calendar date and minute of day an instant falls on in this offer's own time zone. The application
    // layer uses it to pick the right day's stock and to label the next change in the restaurant's clock.
    public (DateOnly Date, int Minute) ToLocal(DateTime utc) => SmartOfferTiming.ToLocal(utc, ResolveTimeZone());

    private static bool IsSameOffer(SmartPriceQuote? a, SmartPriceQuote? b)
    {
        if (a == null || b == null)
        {
            return a == null && b == null;
        }

        return a.SlotId == b.SlotId && a.Price == b.Price;
    }

    private void Apply(
        SmartOfferDetails details,
        SmartOfferPricingSpec pricing,
        SmartOfferScheduleSpec schedule,
        bool isEnabled)
    {
        // Everything is checked before the first assignment below, so a rejected update leaves the offer exactly
        // as it was.
        ValidateDetails(details);
        ValidatePricing(pricing);
        ValidateSchedule(schedule);
        var orderedStages = ValidateStages(pricing);

        TitleAr = Check.NotNullOrWhiteSpace(details.TitleAr, nameof(details.TitleAr), SmartOfferConsts.MaxTitleLength);
        TitleEn = Check.NotNullOrWhiteSpace(details.TitleEn, nameof(details.TitleEn), SmartOfferConsts.MaxTitleLength);
        DescriptionAr = details.DescriptionAr;
        DescriptionEn = details.DescriptionEn;

        Strategy = pricing.Strategy;
        Currency = pricing.Currency;
        BasePrice = pricing.BasePrice;
        MinimumPrice = pricing.MinimumPrice;
        DailyQuantity = pricing.DailyQuantity;

        TimeZoneId = schedule.TimeZoneId;
        ValidFrom = schedule.ValidFrom;
        ValidTo = schedule.ValidTo;

        IsEnabled = isEnabled;

        ReconcileStages(orderedStages);
    }

    private void ReconcileStages(IReadOnlyList<SmartOfferStageSpec> desired)
    {
        var desiredIds = desired.Select(s => s.Id).ToHashSet();
        _stages.RemoveAll(stage => !desiredIds.Contains(stage.Id));

        foreach (var spec in desired)
        {
            var existing = _stages.FirstOrDefault(stage => stage.Id == spec.Id);
            if (existing != null)
            {
                existing.Apply(spec);
            }
            else
            {
                _stages.Add(new SmartOfferPriceStage(spec.Id, Id, spec));
            }
        }
    }

    private static void ValidateDetails(SmartOfferDetails details)
    {
        Check.NotNullOrWhiteSpace(details.TitleAr, nameof(details.TitleAr), SmartOfferConsts.MaxTitleLength);
        Check.NotNullOrWhiteSpace(details.TitleEn, nameof(details.TitleEn), SmartOfferConsts.MaxTitleLength);

        if (details.DescriptionAr?.Length > SmartOfferConsts.MaxDescriptionLength ||
            details.DescriptionEn?.Length > SmartOfferConsts.MaxDescriptionLength)
        {
            throw new UserFriendlyException($"Descriptions can be at most {SmartOfferConsts.MaxDescriptionLength} characters.");
        }
    }

    private static void ValidatePricing(SmartOfferPricingSpec pricing)
    {
        if (!Enum.IsDefined(pricing.Strategy))
        {
            throw new UserFriendlyException("Choose a valid pricing mode.");
        }

        if (!Enum.IsDefined(pricing.Currency))
        {
            throw new UserFriendlyException("Choose a valid currency.");
        }

        ValidateMoney(pricing.BasePrice, "The base price");
        if (pricing.MinimumPrice.HasValue)
        {
            ValidateMoney(pricing.MinimumPrice.Value, "The minimum price");
            if (pricing.MinimumPrice.Value > pricing.BasePrice)
            {
                throw new UserFriendlyException("The minimum price can't be higher than the base price.");
            }
        }

        if (pricing.DailyQuantity.HasValue && !IsValidQuantity(pricing.DailyQuantity.Value))
        {
            throw new UserFriendlyException($"The daily quantity must be between 1 and {SmartOfferConsts.MaxQuantity:N0}.");
        }

        if (pricing.Strategy == SmartPricingStrategy.TimeBased && pricing.DailyQuantity.HasValue)
        {
            throw new UserFriendlyException("Time-based offers set quantity per stage, not per day.");
        }

        if (pricing.Strategy == SmartPricingStrategy.Fixed && pricing.Stages.Count > 0)
        {
            throw new UserFriendlyException("Fixed-price offers can't have pricing stages. Switch to time-based pricing first.");
        }
    }

    private static void ValidateSchedule(SmartOfferScheduleSpec schedule)
    {
        if (string.IsNullOrWhiteSpace(schedule.TimeZoneId) || schedule.TimeZoneId.Length > SmartOfferConsts.MaxTimeZoneIdLength)
        {
            throw new UserFriendlyException("Choose the time zone the offer runs in.");
        }

        SmartOfferTiming.ResolveTimeZone(schedule.TimeZoneId);

        if (schedule.ValidFrom.HasValue && schedule.ValidTo.HasValue && schedule.ValidTo.Value < schedule.ValidFrom.Value)
        {
            throw new UserFriendlyException("The offer's end date can't be before its start date.");
        }
    }

    // Returns the stages sorted by start time, ready for reconciliation. Overlap is checked on the sorted list, so
    // any pair that collides is caught without comparing every combination.
    private static IReadOnlyList<SmartOfferStageSpec> ValidateStages(SmartOfferPricingSpec pricing)
    {
        var stages = pricing.Stages;

        if (pricing.Strategy == SmartPricingStrategy.Fixed)
        {
            return Array.Empty<SmartOfferStageSpec>();
        }

        if (stages.Count == 0)
        {
            throw new UserFriendlyException("A time-based offer needs at least one pricing stage.");
        }

        if (stages.Count > SmartOfferConsts.MaxStagesPerOffer)
        {
            throw new UserFriendlyException($"An offer can have at most {SmartOfferConsts.MaxStagesPerOffer} pricing stages.");
        }

        if (stages.Select(s => s.Id).Distinct().Count() != stages.Count)
        {
            throw new UserFriendlyException("Each pricing stage must be listed only once.");
        }

        foreach (var stage in stages)
        {
            ValidateStage(stage, pricing);
        }

        var ordered = stages.OrderBy(s => s.StartMinute).ToList();
        for (var i = 1; i < ordered.Count; i++)
        {
            if (ordered[i].StartMinute < ordered[i - 1].EndMinute)
            {
                throw new UserFriendlyException(
                    $"Stages can't overlap: {MinuteOfDay.Format(ordered[i - 1].StartMinute)}–{MinuteOfDay.Format(ordered[i - 1].EndMinute)} " +
                    $"and {MinuteOfDay.Format(ordered[i].StartMinute)}–{MinuteOfDay.Format(ordered[i].EndMinute)} overlap.");
            }
        }

        return ordered;
    }

    private static void ValidateStage(SmartOfferStageSpec stage, SmartOfferPricingSpec pricing)
    {
        if (stage.StartMinute < 0 || stage.StartMinute >= SmartOfferConsts.MinutesPerDay)
        {
            throw new UserFriendlyException("A stage must start on a valid time of day.");
        }

        if (stage.EndMinute <= stage.StartMinute || stage.EndMinute > SmartOfferConsts.MinutesPerDay)
        {
            throw new UserFriendlyException(
                $"A stage must end after it starts, but this one ends at or before {MinuteOfDay.Format(stage.StartMinute)}.");
        }

        ValidateMoney(stage.Price, "A stage's price");

        if (stage.Price > pricing.BasePrice)
        {
            throw new UserFriendlyException("A stage's price can't be higher than the base price.");
        }

        if (pricing.MinimumPrice.HasValue && stage.Price < pricing.MinimumPrice.Value)
        {
            throw new UserFriendlyException("A stage's price is below the minimum price.");
        }

        if (!Enum.IsDefined(stage.Currency) || stage.Currency != pricing.Currency)
        {
            // No SYP/USD conversion anywhere in the platform, by design. A stage priced in another currency would
            // have no meaning next to the offer's base price.
            throw new UserFriendlyException("Every stage must use the offer's currency.");
        }

        if (stage.QuantityLimit.HasValue && !IsValidQuantity(stage.QuantityLimit.Value))
        {
            throw new UserFriendlyException($"A stage's quantity must be between 1 and {SmartOfferConsts.MaxQuantity:N0}.");
        }
    }

    private static void ValidateMoney(decimal amount, string label)
    {
        if (amount <= 0m)
        {
            throw new UserFriendlyException($"{label} must be greater than zero.");
        }

        if (amount > SmartOfferConsts.MaxPrice)
        {
            throw new UserFriendlyException($"{label} can't exceed {SmartOfferConsts.MaxPrice:N0}.");
        }

        if (decimal.Round(amount, 2) != amount)
        {
            throw new UserFriendlyException($"{label} can have at most two decimal places.");
        }
    }

    private static bool IsValidQuantity(int quantity) => quantity >= 1 && quantity <= SmartOfferConsts.MaxQuantity;
}
