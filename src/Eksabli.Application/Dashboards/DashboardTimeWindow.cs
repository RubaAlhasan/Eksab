using System;
using System.Collections.Generic;
using Eksabli.SmartOffers;
using Volo.Abp;

namespace Eksabli.Dashboards;

// Turns the calendar-date range a dashboard request sends into the UTC instants its queries filter on. The dates are
// read in whichever zone the caller passes: the business's own clock, or UTC for the Admin dashboard. Never the server's.
public static class DashboardTimeWindow
{
    public static (DateOnly From, DateOnly To) Resolve(DashboardRangeDto input, DateOnly today)
    {
        var to = input.To ?? today;
        var from = input.From ?? to.AddDays(1 - DashboardDefinitions.DefaultRangeDays);

        if (to < from)
        {
            throw new UserFriendlyException("The end date can't be before the start date.");
        }

        if (to.DayNumber - from.DayNumber + 1 > DashboardDefinitions.MaxRangeDays)
        {
            throw new UserFriendlyException($"A dashboard can cover at most {DashboardDefinitions.MaxRangeDays} days.");
        }

        return (from, to);
    }

    // The half-open UTC interval [StartUtc, EndUtc) covering the local days From..To, both inclusive.
    public static (DateTime StartUtc, DateTime EndUtc) ToUtcBounds(DateOnly from, DateOnly to, TimeZoneInfo zone)
    {
        return (SmartOfferTiming.ToUtc(from, 0, zone), SmartOfferTiming.ToUtc(to.AddDays(1), 0, zone));
    }

    public static DateOnly TodayIn(DateTime utcNow, TimeZoneInfo zone) => SmartOfferTiming.ToLocal(utcNow, zone).Date;

    public static DateOnly LocalDateOf(DateTime utc, TimeZoneInfo zone) => SmartOfferTiming.ToLocal(utc, zone).Date;

    public static IEnumerable<DateOnly> EachDay(DateOnly from, DateOnly to)
    {
        for (var day = from; day <= to; day = day.AddDays(1))
        {
            yield return day;
        }
    }
}
