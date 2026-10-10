using System;

namespace Eksabli.Dashboards;

// Both bounds are calendar dates, inclusive. The Business dashboard reads them on the business's own clock
// (BusinessProfile.TimeZoneId); the Admin dashboard reads them in UTC. Null on either side means the default
// window: the last DefaultRangeDays days, ending today. The window is capped at MaxRangeDays.
public class DashboardRangeDto
{
    public DateOnly? From { get; set; }

    public DateOnly? To { get; set; }
}
