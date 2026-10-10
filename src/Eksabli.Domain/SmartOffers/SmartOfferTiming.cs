using System;
using Volo.Abp;

namespace Eksabli.SmartOffers;

// All pricing rules are evaluated in the offer's own IANA time zone (a restaurant's local clock), never in
// the server's. Instants are UTC DateTimes; a "local date + minute of day" is what a stage is defined in.
public static class SmartOfferTiming
{
    public static TimeZoneInfo ResolveTimeZone(string timeZoneId)
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            throw new UserFriendlyException($"'{timeZoneId}' is not a recognised time zone.");
        }
    }

    // The local calendar date and minute-of-day that a UTC instant falls on in the given zone.
    public static (DateOnly Date, int Minute) ToLocal(DateTime utc, TimeZoneInfo timeZone)
    {
        var local = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), timeZone);
        return (DateOnly.FromDateTime(local), local.Hour * 60 + local.Minute);
    }

    // The UTC instant of a local (date, minute-of-day). minuteOfDay may be 1440, which resolves to the next
    // local midnight, so a stage ending at "24:00" has a real end instant.
    //
    // DST spring-forward creates local times that never occur. Those are pushed forward to the first valid
    // minute, so a stage that starts inside the gap begins the moment the clocks jump. Fall-back ambiguity is
    // left to TimeZoneInfo's own resolution.
    public static DateTime ToUtc(DateOnly date, int minuteOfDay, TimeZoneInfo timeZone)
    {
        var local = date.ToDateTime(TimeOnly.MinValue).AddMinutes(minuteOfDay);
        while (timeZone.IsInvalidTime(local))
        {
            local = local.AddMinutes(1);
        }

        return TimeZoneInfo.ConvertTimeToUtc(local, timeZone);
    }
}
