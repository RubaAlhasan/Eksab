using System;
using System.Collections.Generic;
using System.Linq;
using Eksabli.Shared;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace Eksabli.SmartOffers;

// Pure domain tests: every instant is passed explicitly, so no test depends on the real clock or on the machine's zone.
public class SmartOfferPricingTests
{
    // UTC+3 with no DST since 2022, so these expected UTC instants are stable.
    private const string DamascusZone = "Asia/Damascus";

    private static readonly SmartOfferDetails Details = new("برغر", "Burger Meal", null, null);

    // Local wall-clock time in Damascus on 2026-10-04 (plus dayOffset days), as a UTC instant.
    private static DateTime Local(int hour, int minute = 0, int dayOffset = 0) =>
        new DateTime(2026, 10, 4, 0, 0, 0, DateTimeKind.Utc).AddDays(dayOffset).AddHours(hour - 3).AddMinutes(minute);

    private static SmartOfferStageSpec Stage(int startMinute, int endMinute, decimal price, int? quantity = null, Currency currency = Currency.Usd) =>
        new(Guid.NewGuid(), startMinute, endMinute, price, currency, quantity);

    private static SmartOffer TimeBasedOffer(
        IEnumerable<SmartOfferStageSpec> stages,
        decimal basePrice = 10m,
        decimal? minimumPrice = null,
        string timeZone = DamascusZone,
        DateOnly? from = null,
        DateOnly? to = null,
        bool enabled = true)
    {
        return SmartOffer.Create(
            Guid.NewGuid(),
            Details,
            new SmartOfferPricingSpec(SmartPricingStrategy.TimeBased, Currency.Usd, basePrice, minimumPrice, null, stages.ToList()),
            new SmartOfferScheduleSpec(timeZone, from, to),
            enabled);
    }

    private static SmartOffer FixedOffer(decimal price, int? dailyQuantity = null, DateOnly? from = null, DateOnly? to = null) =>
        SmartOffer.Create(
            Guid.NewGuid(),
            Details,
            new SmartOfferPricingSpec(SmartPricingStrategy.Fixed, Currency.Usd, price, null, dailyQuantity, Array.Empty<SmartOfferStageSpec>()),
            new SmartOfferScheduleSpec(DamascusZone, from, to),
            true);

    // The four-stage example from the feature brief: 09–11 $10, 11–13 $7, 13–15 $5, 15–17 $3.
    private static SmartOffer BriefExample() => TimeBasedOffer(new[]
    {
        Stage(9 * 60, 11 * 60, 10m),
        Stage(11 * 60, 13 * 60, 7m),
        Stage(13 * 60, 15 * 60, 5m),
        Stage(15 * 60, 17 * 60, 3m),
    });

    // ---- Fixed pricing -----------------------------------------------------------------------------------------------

    [Fact]
    public void Fixed_offer_quotes_its_base_price_through_the_day_and_a_daily_quantity()
    {
        var offer = FixedOffer(8m, dailyQuantity: 20, from: new DateOnly(2026, 10, 4), to: new DateOnly(2026, 10, 5));

        var quote = offer.Quote(Local(12));

        quote.ShouldNotBeNull();
        quote.Price.ShouldBe(8m);
        quote.BasePrice.ShouldBe(8m);
        quote.QuantityLimit.ShouldBe(20);
        quote.StageId.ShouldBeNull();
        quote.SlotId.ShouldBe(offer.Id);
        quote.WindowStartUtc.ShouldBe(Local(0));
        quote.WindowEndUtc.ShouldBe(Local(0, dayOffset: 1));
    }

    [Fact]
    public void Fixed_offer_validity_is_inclusive_of_both_local_dates_and_closed_outside_them()
    {
        var offer = FixedOffer(8m, from: new DateOnly(2026, 10, 4), to: new DateOnly(2026, 10, 5));

        offer.Quote(Local(23, 59, dayOffset: 1)).ShouldNotBeNull();   // last minute of the ValidTo day
        offer.Quote(Local(0, 0, dayOffset: 2)).ShouldBeNull();        // first minute after it
        offer.Quote(Local(0, 0, dayOffset: -1)).ShouldBeNull();       // day before ValidFrom
    }

    // ---- Time-based pricing ------------------------------------------------------------------------------------------

    [Fact]
    public void A_stage_includes_its_start_minute_and_excludes_its_end_minute()
    {
        var offer = TimeBasedOffer(new[] { Stage(9 * 60, 11 * 60, 10m) });

        offer.Quote(Local(9, 0)).ShouldNotBeNull();
        offer.Quote(Local(10, 59)).ShouldNotBeNull();
        offer.Quote(Local(11, 0)).ShouldBeNull();
    }

    [Fact]
    public void Adjacent_stages_hand_over_exactly_at_the_boundary()
    {
        var offer = BriefExample();

        offer.Quote(Local(10, 59)).Price.ShouldBe(10m);

        var handover = offer.Quote(Local(11, 0));
        handover.ShouldNotBeNull();
        handover.Price.ShouldBe(7m);
    }

    [Fact]
    public void A_gap_between_stages_is_not_on_sale()
    {
        var offer = TimeBasedOffer(new[]
        {
            Stage(9 * 60, 11 * 60, 10m),
            Stage(13 * 60, 15 * 60, 5m),
        });

        offer.Quote(Local(12)).ShouldBeNull();
    }

    [Fact]
    public void The_quote_window_is_the_stage_expressed_as_utc_instants()
    {
        var offer = BriefExample();

        var quote = offer.Quote(Local(9, 30));

        quote.ShouldNotBeNull();
        quote.WindowStartUtc.ShouldBe(Local(9));
        quote.WindowEndUtc.ShouldBe(Local(11));
        quote.StageId.ShouldNotBeNull();
        quote.SlotId.ShouldBe(quote.StageId.Value);
    }

    [Fact]
    public void A_stage_ending_at_midnight_runs_to_the_next_local_midnight()
    {
        var offer = TimeBasedOffer(new[] { Stage(22 * 60, MinuteOfDay.Parse("24:00"), 5m) });

        offer.Quote(Local(23, 59)).ShouldNotBeNull();
        offer.Quote(Local(23, 59)).WindowEndUtc.ShouldBe(Local(0, dayOffset: 1));
    }

    [Fact]
    public void Prices_follow_the_restaurant_clock_not_the_server_clock()
    {
        var offer = TimeBasedOffer(new[] { Stage(9 * 60, 11 * 60, 10m) });

        // 06:30 UTC is 09:30 in Damascus: on sale. 08:30 UTC is 11:30 there: not.
        offer.Quote(new DateTime(2026, 10, 4, 6, 30, 0, DateTimeKind.Utc)).ShouldNotBeNull();
        offer.Quote(new DateTime(2026, 10, 4, 8, 30, 0, DateTimeKind.Utc)).ShouldBeNull();
    }

    [Fact]
    public void Validity_is_decided_by_the_local_calendar_date_not_the_utc_date()
    {
        // Valid from 2026-10-05 local. 21:30 UTC on the 4th is 00:30 on the 5th in Damascus, so it is already in force.
        var offer = TimeBasedOffer(
            new[] { Stage(0, 60, 3m) },
            from: new DateOnly(2026, 10, 5));

        offer.Quote(Local(23, 30)).ShouldBeNull();               // 2026-10-04 23:30 local: before ValidFrom
        offer.Quote(new DateTime(2026, 10, 4, 21, 30, 0, DateTimeKind.Utc)).ShouldNotBeNull();
    }

    [Fact]
    public void A_stage_starting_inside_a_daylight_saving_gap_begins_when_the_clocks_jump()
    {
        // 2026-03-08: New York springs forward 02:00 -> 03:00 (EST -> EDT). 02:00 never occurs that day.
        var offer = SmartOffer.Create(
            Guid.NewGuid(),
            Details,
            new SmartOfferPricingSpec(
                SmartPricingStrategy.TimeBased,
                Currency.Usd,
                10m,
                null,
                null,
                new[] { Stage(2 * 60, 3 * 60 + 30, 6m) }),
            new SmartOfferScheduleSpec("America/New_York", new DateOnly(2026, 3, 8), new DateOnly(2026, 3, 8)),
            true);

        var atJump = new DateTime(2026, 3, 8, 7, 0, 0, DateTimeKind.Utc); // 03:00 EDT
        var quote = offer.Quote(atJump);

        quote.ShouldNotBeNull();
        quote.WindowStartUtc.ShouldBe(atJump);
        offer.Quote(new DateTime(2026, 3, 8, 6, 59, 0, DateTimeKind.Utc)).ShouldBeNull(); // 01:59 EST, before the stage
    }

    [Fact]
    public void A_disabled_offer_quotes_nothing_even_inside_a_live_stage()
    {
        var offer = TimeBasedOffer(new[] { Stage(9 * 60, 11 * 60, 10m) }, enabled: false);

        offer.Quote(Local(9, 30)).ShouldBeNull();
    }

    // ---- Next price change -------------------------------------------------------------------------------------------

    [Fact]
    public void The_next_change_is_the_next_stage_start_and_its_price()
    {
        var change = BriefExample().GetNextChange(Local(10, 30));

        change.ShouldNotBeNull();
        change.AtUtc.ShouldBe(Local(11));
        change.Price.ShouldBe(7m);
    }

    [Fact]
    public void Running_out_of_a_stage_into_a_gap_is_reported_as_going_off_sale()
    {
        var offer = TimeBasedOffer(new[]
        {
            Stage(9 * 60, 11 * 60, 10m),
            Stage(13 * 60, 15 * 60, 5m),
        });

        var change = offer.GetNextChange(Local(10, 30));

        change.ShouldNotBeNull();
        change.AtUtc.ShouldBe(Local(11));
        change.Price.ShouldBeNull();
    }

    [Fact]
    public void Inside_a_gap_the_next_change_is_the_next_stage_start()
    {
        var offer = TimeBasedOffer(new[]
        {
            Stage(9 * 60, 11 * 60, 10m),
            Stage(13 * 60, 15 * 60, 5m),
        });

        var change = offer.GetNextChange(Local(11, 30));

        change.ShouldNotBeNull();
        change.AtUtc.ShouldBe(Local(13));
        change.Price.ShouldBe(5m);
    }

    [Fact]
    public void After_the_last_stage_of_the_day_the_next_change_is_tomorrows_first_stage()
    {
        var offer = TimeBasedOffer(new[] { Stage(9 * 60, 11 * 60, 10m) });

        var change = offer.GetNextChange(Local(12));

        change.ShouldNotBeNull();
        change.AtUtc.ShouldBe(Local(9, dayOffset: 1));
        change.Price.ShouldBe(10m);
    }

    // ---- Lifecycle status --------------------------------------------------------------------------------------------

    [Fact]
    public void Status_reflects_enabled_validity_and_whether_a_stage_is_live()
    {
        var live = TimeBasedOffer(new[] { Stage(9 * 60, 11 * 60, 10m) });
        live.GetStatus(Local(9, 30)).ShouldBe(SmartOfferStatus.Live);
        live.GetStatus(Local(12)).ShouldBe(SmartOfferStatus.BetweenStages);

        live.SetEnabled(false);
        live.GetStatus(Local(9, 30)).ShouldBe(SmartOfferStatus.Paused);

        var scheduled = TimeBasedOffer(new[] { Stage(9 * 60, 11 * 60, 10m) }, from: new DateOnly(2026, 10, 10));
        scheduled.GetStatus(Local(9, 30)).ShouldBe(SmartOfferStatus.Scheduled);

        var expired = TimeBasedOffer(new[] { Stage(9 * 60, 11 * 60, 10m) }, to: new DateOnly(2026, 10, 1));
        expired.GetStatus(Local(9, 30)).ShouldBe(SmartOfferStatus.Expired);
    }

    // ---- Configuration validation ------------------------------------------------------------------------------------

    [Fact]
    public void Overlapping_stages_are_rejected()
    {
        Should.Throw<UserFriendlyException>(() => TimeBasedOffer(new[]
        {
            Stage(9 * 60, 11 * 60, 10m),
            Stage(10 * 60, 12 * 60, 7m),
        }));
    }

    [Fact]
    public void A_stage_must_end_after_it_starts()
    {
        Should.Throw<UserFriendlyException>(() => TimeBasedOffer(new[] { Stage(11 * 60, 9 * 60, 10m) }));
        Should.Throw<UserFriendlyException>(() => TimeBasedOffer(new[] { Stage(9 * 60, 9 * 60, 10m) }));
    }

    [Fact]
    public void A_stage_price_above_the_base_price_is_rejected()
    {
        Should.Throw<UserFriendlyException>(() => TimeBasedOffer(new[] { Stage(9 * 60, 11 * 60, 11m) }, basePrice: 10m));
    }

    [Fact]
    public void A_stage_price_below_the_minimum_price_is_rejected()
    {
        Should.Throw<UserFriendlyException>(() => TimeBasedOffer(new[] { Stage(9 * 60, 11 * 60, 2m) }, basePrice: 10m, minimumPrice: 3m));
    }

    [Fact]
    public void A_stage_in_a_different_currency_from_the_offer_is_rejected()
    {
        Should.Throw<UserFriendlyException>(() => TimeBasedOffer(new[]
        {
            new SmartOfferStageSpec(Guid.NewGuid(), 9 * 60, 11 * 60, 10m, Currency.Syp, null),
        }));
    }

    [Fact]
    public void A_time_based_offer_needs_at_least_one_stage_and_a_fixed_offer_must_not_have_any()
    {
        Should.Throw<UserFriendlyException>(() => TimeBasedOffer(Array.Empty<SmartOfferStageSpec>()));

        Should.Throw<UserFriendlyException>(() => SmartOffer.Create(
            Guid.NewGuid(),
            Details,
            new SmartOfferPricingSpec(SmartPricingStrategy.Fixed, Currency.Usd, 8m, null, null, new[] { Stage(9 * 60, 11 * 60, 6m) }),
            new SmartOfferScheduleSpec(DamascusZone, null, null),
            true));
    }

    [Fact]
    public void More_stages_than_the_limit_are_rejected()
    {
        var stages = Enumerable.Range(0, SmartOfferConsts.MaxStagesPerOffer + 1)
            .Select(hour => Stage(hour * 60, hour * 60 + 60, 5m))
            .ToList();

        Should.Throw<UserFriendlyException>(() => TimeBasedOffer(stages));
    }

    [Fact]
    public void A_duplicated_stage_id_is_rejected()
    {
        var shared = Guid.NewGuid();

        Should.Throw<UserFriendlyException>(() => TimeBasedOffer(new[]
        {
            new SmartOfferStageSpec(shared, 9 * 60, 10 * 60, 10m, Currency.Usd, null),
            new SmartOfferStageSpec(shared, 11 * 60, 12 * 60, 7m, Currency.Usd, null),
        }));
    }

    [Fact]
    public void Prices_must_be_positive_and_have_at_most_two_decimal_places()
    {
        Should.Throw<UserFriendlyException>(() => TimeBasedOffer(new[] { Stage(9 * 60, 11 * 60, 0m) }));
        Should.Throw<UserFriendlyException>(() => TimeBasedOffer(new[] { Stage(9 * 60, 11 * 60, 10.123m) }, basePrice: 20m));
        Should.Throw<UserFriendlyException>(() => SmartOffer.Create(
            Guid.NewGuid(),
            Details,
            new SmartOfferPricingSpec(SmartPricingStrategy.Fixed, Currency.Usd, 8.555m, null, null, Array.Empty<SmartOfferStageSpec>()),
            new SmartOfferScheduleSpec(DamascusZone, null, null),
            true));
    }

    [Fact]
    public void An_unknown_time_zone_is_rejected()
    {
        Should.Throw<UserFriendlyException>(() => TimeBasedOffer(new[] { Stage(9 * 60, 11 * 60, 10m) }, timeZone: "Mars/Olympus"));
    }

    [Fact]
    public void An_end_date_before_the_start_date_is_rejected()
    {
        Should.Throw<UserFriendlyException>(() => TimeBasedOffer(
            new[] { Stage(9 * 60, 11 * 60, 10m) },
            from: new DateOnly(2026, 10, 10),
            to: new DateOnly(2026, 10, 1)));
    }

    [Fact]
    public void A_time_based_offer_cannot_carry_a_daily_quantity_because_quantity_is_set_per_stage()
    {
        Should.Throw<UserFriendlyException>(() => SmartOffer.Create(
            Guid.NewGuid(),
            Details,
            new SmartOfferPricingSpec(SmartPricingStrategy.TimeBased, Currency.Usd, 10m, null, 5, new[] { Stage(9 * 60, 11 * 60, 7m) }),
            new SmartOfferScheduleSpec(DamascusZone, null, null),
            true));
    }

    [Fact]
    public void A_rejected_update_leaves_the_offer_exactly_as_it_was()
    {
        var offer = BriefExample();
        var stagesBefore = offer.Stages.Count;

        Should.Throw<UserFriendlyException>(() => offer.Update(
            Details,
            new SmartOfferPricingSpec(
                SmartPricingStrategy.TimeBased,
                Currency.Usd,
                99m,
                null,
                null,
                new[] { Stage(9 * 60, 11 * 60, 10m), Stage(10 * 60, 12 * 60, 7m) }),
            new SmartOfferScheduleSpec(DamascusZone, null, null),
            true));

        offer.BasePrice.ShouldBe(10m);
        offer.Stages.Count.ShouldBe(stagesBefore);
    }

    [Fact]
    public void Updating_keeps_the_identity_of_surviving_stages_and_drops_removed_ones()
    {
        var offer = BriefExample();
        var kept = offer.Stages.OrderBy(s => s.StartMinute).First();
        var removed = offer.Stages.OrderBy(s => s.StartMinute).Last();

        var newStage = Stage(17 * 60, 19 * 60, 2m);
        offer.Update(
            Details,
            new SmartOfferPricingSpec(
                SmartPricingStrategy.TimeBased,
                Currency.Usd,
                10m,
                null,
                null,
                new[]
                {
                    new SmartOfferStageSpec(kept.Id, kept.StartMinute, kept.EndMinute, 9m, Currency.Usd, 4),
                    new SmartOfferStageSpec(Guid.NewGuid(), 11 * 60, 13 * 60, 7m, Currency.Usd, null),
                    newStage,
                }),
            new SmartOfferScheduleSpec(DamascusZone, null, null),
            true);

        offer.Stages.ShouldContain(s => s.Id == kept.Id && s.Price == 9m && s.QuantityLimit == 4);
        offer.Stages.ShouldNotContain(s => s.Id == removed.Id);
        offer.Stages.Count.ShouldBe(3);
    }

    // ---- Minute-of-day helper ----------------------------------------------------------------------------------------

    [Theory]
    [InlineData("09:30", 570)]
    [InlineData("00:00", 0)]
    [InlineData("23:59", 1439)]
    [InlineData("24:00", 1440)]
    public void Minute_of_day_parses_valid_times(string text, int expected)
    {
        MinuteOfDay.Parse(text).ShouldBe(expected);
    }

    [Theory]
    [InlineData("24:30")]
    [InlineData("25:00")]
    [InlineData("12:60")]
    [InlineData("nine")]
    [InlineData("")]
    public void Minute_of_day_rejects_invalid_times(string text)
    {
        MinuteOfDay.TryParse(text, out _).ShouldBeFalse();
    }

    [Fact]
    public void Minute_of_day_formats_back_to_hh_mm()
    {
        MinuteOfDay.Format(570).ShouldBe("09:30");
        MinuteOfDay.Format(1440).ShouldBe("24:00");
    }
}
