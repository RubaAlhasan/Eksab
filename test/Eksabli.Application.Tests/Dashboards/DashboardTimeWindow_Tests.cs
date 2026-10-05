using System;
using System.Linq;
using Eksabli.Shared;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace Eksabli.Dashboards;

// Pure date and money rules, no database. The Damascus zone is UTC+3 with no daylight saving since 2022, so a local
// midnight is always 21:00 UTC the day before. That's the boundary the Business dashboard's "today" depends on.
public class DashboardTimeWindow_Tests
{
    private static readonly TimeZoneInfo Damascus = TimeZoneInfo.FindSystemTimeZoneById("Asia/Damascus");

    [Fact]
    public void Resolve_Should_Default_To_The_Last_Thirty_Days_Ending_Today()
    {
        var today = new DateOnly(2026, 10, 5);

        var (from, to) = DashboardTimeWindow.Resolve(new DashboardRangeDto(), today);

        to.ShouldBe(today);
        from.ShouldBe(today.AddDays(-29));
    }

    [Fact]
    public void Resolve_Should_Reject_An_End_Date_Before_The_Start_Date()
    {
        var today = new DateOnly(2026, 10, 5);

        Should.Throw<UserFriendlyException>(() => DashboardTimeWindow.Resolve(
            new DashboardRangeDto { From = today, To = today.AddDays(-1) }, today));
    }

    [Fact]
    public void Resolve_Should_Accept_Exactly_The_Maximum_Span_And_Reject_One_Day_More()
    {
        var today = new DateOnly(2026, 10, 5);

        var atLimit = DashboardTimeWindow.Resolve(
            new DashboardRangeDto { From = today.AddDays(1 - DashboardDefinitions.MaxRangeDays), To = today }, today);
        atLimit.From.ShouldBe(today.AddDays(1 - DashboardDefinitions.MaxRangeDays));

        Should.Throw<UserFriendlyException>(() => DashboardTimeWindow.Resolve(
            new DashboardRangeDto { From = today.AddDays(-DashboardDefinitions.MaxRangeDays), To = today }, today));
    }

    [Fact]
    public void ToUtcBounds_Should_Start_At_Local_Midnight_Not_UTC_Midnight()
    {
        var day = new DateOnly(2026, 10, 5);

        var (start, end) = DashboardTimeWindow.ToUtcBounds(day, day, Damascus);

        start.ShouldBe(new DateTime(2026, 10, 4, 21, 0, 0, DateTimeKind.Utc));
        end.ShouldBe(new DateTime(2026, 10, 5, 21, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void LocalDateOf_Should_Put_A_Late_Utc_Sale_On_The_Next_Local_Day()
    {
        // 22:30 UTC is 01:30 on the 5th in Damascus. A UTC-based report would still count it as the 4th.
        var sale = new DateTime(2026, 10, 4, 22, 30, 0, DateTimeKind.Utc);

        DashboardTimeWindow.LocalDateOf(sale, Damascus).ShouldBe(new DateOnly(2026, 10, 5));
    }

    [Fact]
    public void EachDay_Should_Yield_Every_Calendar_Day_Inclusive()
    {
        var days = DashboardTimeWindow.EachDay(new DateOnly(2026, 2, 27), new DateOnly(2026, 3, 1)).ToList();

        // 2026 isn't a leap year, so the 27th is followed by the 28th, then March.
        days.ShouldBe(new[]
        {
            new DateOnly(2026, 2, 27),
            new DateOnly(2026, 2, 28),
            new DateOnly(2026, 3, 1),
        });
    }

    [Fact]
    public void FlowTotals_Sum_Should_Keep_Each_Currency_Separate()
    {
        var syp = new FlowTotals();
        syp.RecordedValue[Currency.Syp] = 100_000m;
        syp.PointsIssued = 10;

        var usd = new FlowTotals();
        usd.RecordedValue[Currency.Usd] = 40m;
        usd.PointsIssued = 5;

        var total = FlowTotals.Sum(new[] { syp, usd });

        // Points are one unit across currencies, so they add up. Money is never converted or added across currencies.
        total.PointsIssued.ShouldBe(15);
        total.RecordedValueAmounts().Count.ShouldBe(2);
        total.RecordedValue[Currency.Syp].ShouldBe(100_000m);
        total.RecordedValue[Currency.Usd].ShouldBe(40m);
    }
}
