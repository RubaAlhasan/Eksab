using System;
using System.Collections.Generic;
using Shouldly;
using Xunit;

namespace Eksabli.Branches;

// UTC throughout — the time-zone conversion itself is SmartOfferTiming's own concern (and is exercised
// by SmartOffer's pricing tests); these tests are about the weekly-schedule math on top of it.
public class BranchOpeningHoursTests
{
    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;

    // Wednesday 2026-10-07, 09:00-18:00 only.
    private static List<BranchDaySchedule> WeekdayOnly() => new()
    {
        new BranchDaySchedule(DayOfWeek.Wednesday, IsClosed: false, OpenMinute: 9 * 60, CloseMinute: 18 * 60),
    };

    [Fact]
    public void Is_Open_During_Its_Own_Window()
    {
        var week = WeekdayOnly();
        var atNoon = new DateTime(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);

        BranchOpeningHours.IsOpenAt(week, atNoon, Utc).ShouldBeTrue();
    }

    [Fact]
    public void Is_Closed_Before_Opening_And_At_Closing_Time()
    {
        var week = WeekdayOnly();
        var beforeOpen = new DateTime(2026, 10, 7, 8, 59, 0, DateTimeKind.Utc);
        var atClose = new DateTime(2026, 10, 7, 18, 0, 0, DateTimeKind.Utc); // exclusive end

        BranchOpeningHours.IsOpenAt(week, beforeOpen, Utc).ShouldBeFalse();
        BranchOpeningHours.IsOpenAt(week, atClose, Utc).ShouldBeFalse();
    }

    [Fact]
    public void Is_Closed_On_A_Day_Not_Listed_In_The_Week()
    {
        var week = WeekdayOnly();
        var thursdayNoon = new DateTime(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc);

        BranchOpeningHours.IsOpenAt(week, thursdayNoon, Utc).ShouldBeFalse();
    }

    [Fact]
    public void An_Overnight_Window_Stays_Open_Past_Local_Midnight()
    {
        // Friday 22:00 to Saturday 02:00.
        var week = new List<BranchDaySchedule>
        {
            new(DayOfWeek.Friday, IsClosed: false, OpenMinute: 22 * 60, CloseMinute: 2 * 60),
        };

        var fridayLateNight = new DateTime(2026, 10, 9, 23, 30, 0, DateTimeKind.Utc); // Friday
        var saturdayEarlyHours = new DateTime(2026, 10, 10, 1, 30, 0, DateTimeKind.Utc); // Saturday, still inside the window
        var saturdayAfterClose = new DateTime(2026, 10, 10, 2, 30, 0, DateTimeKind.Utc); // Saturday, past 02:00

        BranchOpeningHours.IsOpenAt(week, fridayLateNight, Utc).ShouldBeTrue();
        BranchOpeningHours.IsOpenAt(week, saturdayEarlyHours, Utc).ShouldBeTrue();
        BranchOpeningHours.IsOpenAt(week, saturdayAfterClose, Utc).ShouldBeFalse();
    }

    [Fact]
    public void Open_All_Day_Is_Represented_As_Midnight_To_Midnight_Not_As_Overnight()
    {
        var week = new List<BranchDaySchedule>
        {
            new(DayOfWeek.Wednesday, IsClosed: false, OpenMinute: 0, CloseMinute: 24 * 60),
        };

        var justBeforeMidnight = new DateTime(2026, 10, 7, 23, 59, 0, DateTimeKind.Utc);
        var justAfterMidnightNextDay = new DateTime(2026, 10, 8, 0, 1, 0, DateTimeKind.Utc); // Thursday — not listed

        BranchOpeningHours.IsOpenAt(week, justBeforeMidnight, Utc).ShouldBeTrue();
        BranchOpeningHours.IsOpenAt(week, justAfterMidnightNextDay, Utc).ShouldBeFalse();
    }

    [Fact]
    public void Next_Change_Is_The_Closing_Time_While_Open()
    {
        var week = WeekdayOnly();
        var atNoon = new DateTime(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);

        var next = BranchOpeningHours.GetNextChangeUtc(week, atNoon, Utc);

        next.ShouldBe(new DateTime(2026, 10, 7, 18, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void Next_Change_Is_The_Following_Weeks_Opening_Time_While_Closed()
    {
        var week = WeekdayOnly();
        var thursdayNoon = new DateTime(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc);

        var next = BranchOpeningHours.GetNextChangeUtc(week, thursdayNoon, Utc);

        // Next Wednesday, 09:00.
        next.ShouldBe(new DateTime(2026, 10, 14, 9, 0, 0, DateTimeKind.Utc));
    }

    [Fact]
    public void Next_Change_Is_Null_When_No_Day_Is_Ever_Open()
    {
        var week = new List<BranchDaySchedule>
        {
            new(DayOfWeek.Wednesday, IsClosed: true, OpenMinute: 0, CloseMinute: 0),
        };

        BranchOpeningHours.GetNextChangeUtc(week, DateTime.UtcNow, Utc).ShouldBeNull();
    }
}
