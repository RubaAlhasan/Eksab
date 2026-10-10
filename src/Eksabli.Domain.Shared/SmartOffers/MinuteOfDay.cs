using System;
using System.Globalization;

namespace Eksabli.SmartOffers;

// Stage boundaries are stored as minutes since local midnight. Minutes rather than TimeOnly because
// "24:00" (a stage that runs to midnight) is a real end time and TimeOnly cannot represent it.
public static class MinuteOfDay
{
    public static int Parse(string value)
    {
        if (!TryParse(value, out var minutes))
        {
            throw new ArgumentException($"'{value}' is not a valid HH:mm time.", nameof(value));
        }

        return minutes;
    }

    public static bool TryParse(string? value, out int minutes)
    {
        minutes = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var parts = value.Trim().Split(':');
        if (parts.Length != 2 ||
            !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var hours) ||
            !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var mins) ||
            mins is < 0 or > 59)
        {
            return false;
        }

        // "24:00" is the only legal hour of 24 — it's the exclusive end of a stage that runs to midnight.
        if (hours == 24 && mins == 0)
        {
            minutes = SmartOfferConsts.MinutesPerDay;
            return true;
        }

        if (hours is < 0 or > 23)
        {
            return false;
        }

        minutes = hours * 60 + mins;
        return true;
    }

    public static string Format(int minutes)
    {
        var hours = minutes / 60;
        var mins = minutes % 60;
        return string.Create(CultureInfo.InvariantCulture, $"{hours:00}:{mins:00}");
    }
}
