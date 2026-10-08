using System;
using System.Collections.Generic;
using System.Linq;
using Eksabli.SmartOffers;

namespace Eksabli.Branches;

// One day's window in a branch's weekly opening hours. OpenMinute/CloseMinute are minutes since local
// midnight — the same "24:00 is a real end time" convention SmartOffers' MinuteOfDay already uses, so
// "open all day" is OpenMinute=0, CloseMinute=1440, not a special case. CloseMinute <= OpenMinute means
// the branch stays open past local midnight (e.g. 22:00-02:00).
public readonly record struct BranchDaySchedule(DayOfWeek DayOfWeek, bool IsClosed, int OpenMinute, int CloseMinute);

// Pure time math, kept separate from Branch itself the same way SmartOfferTiming is kept separate from
// SmartOffer. A branch's hours are evaluated against the owning business's own time zone
// (BusinessProfile.TimeZoneId) — a branch doesn't carry its own — so that zone is a parameter here,
// never read off the entity.
public static class BranchOpeningHours
{
    public static bool IsOpenAt(IReadOnlyList<BranchDaySchedule> week, DateTime utcNow, TimeZoneInfo timeZone)
    {
        var byDay = OpenDaysByWeekday(week);
        var (today, _) = SmartOfferTiming.ToLocal(utcNow, timeZone);

        // Yesterday's window first — it's the one that can still be open past local midnight — then today's.
        foreach (var offset in new[] { -1, 0 })
        {
            var date = today.AddDays(offset);
            if (!byDay.TryGetValue(date.DayOfWeek, out var schedule)) continue;

            var (openUtc, closeUtc) = Window(date, schedule, timeZone);
            if (utcNow >= openUtc && utcNow < closeUtc) return true;
        }

        return false;
    }

    // The next instant the open/closed state flips, for a "Closes at…"/"Opens…" display — null when the
    // branch has no open day anywhere in its schedule (nothing will ever change). Scans a bounded 10-day
    // window (yesterday through 8 days ahead), always enough to find the next boundary for any
    // weekly-recurring schedule.
    public static DateTime? GetNextChangeUtc(IReadOnlyList<BranchDaySchedule> week, DateTime utcNow, TimeZoneInfo timeZone)
    {
        var byDay = OpenDaysByWeekday(week);
        if (byDay.Count == 0) return null;

        var (today, _) = SmartOfferTiming.ToLocal(utcNow, timeZone);

        var upcoming = new List<DateTime>();
        for (var offset = -1; offset <= 8; offset++)
        {
            var date = today.AddDays(offset);
            if (!byDay.TryGetValue(date.DayOfWeek, out var schedule)) continue;

            var (openUtc, closeUtc) = Window(date, schedule, timeZone);
            if (openUtc > utcNow) upcoming.Add(openUtc);
            if (closeUtc > utcNow) upcoming.Add(closeUtc);
        }

        return upcoming.Count == 0 ? null : upcoming.Min();
    }

    private static Dictionary<DayOfWeek, BranchDaySchedule> OpenDaysByWeekday(IReadOnlyList<BranchDaySchedule> week) =>
        week.Where(d => !d.IsClosed).ToDictionary(d => d.DayOfWeek);

    private static (DateTime OpenUtc, DateTime CloseUtc) Window(DateOnly date, BranchDaySchedule schedule, TimeZoneInfo timeZone)
    {
        var closeMinute = schedule.CloseMinute > schedule.OpenMinute
            ? schedule.CloseMinute
            : schedule.CloseMinute + SmartOfferConsts.MinutesPerDay;

        return (
            SmartOfferTiming.ToUtc(date, schedule.OpenMinute, timeZone),
            SmartOfferTiming.ToUtc(date, closeMinute, timeZone));
    }
}
