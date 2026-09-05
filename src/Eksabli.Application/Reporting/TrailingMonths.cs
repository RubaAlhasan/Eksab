using System;
using System.Collections.Generic;

namespace Eksabli.Reporting;

// Shared "trailing N months, this month inclusive" window used by every monthly trend chart in the
// Admin Portal (Platform MRR, Tenant Growth, ...) — extracted so the date-window/cursor-loop logic
// lives in one place instead of being copy-pasted per trend (each caller still does its own DB-level
// GroupBy and zero-fills by mapping over this list, since the aggregated value type differs per trend).
public static class TrailingMonths
{
    public static List<(int Year, int Month)> Compute(DateTime now, int monthsBack = 6)
    {
        var from = new DateTime(now.Year, now.Month, 1).AddMonths(-monthsBack);
        var end = new DateTime(now.Year, now.Month, 1);

        var months = new List<(int Year, int Month)>();
        var cursor = from;
        while (cursor <= end)
        {
            months.Add((cursor.Year, cursor.Month));
            cursor = cursor.AddMonths(1);
        }

        return months;
    }
}
