using System;
using System.ComponentModel.DataAnnotations;

namespace Eksabli.Branches;

public class DayOpeningHoursDto
{
    public DayOfWeek DayOfWeek { get; set; }

    public bool IsClosed { get; set; }

    // "HH:mm", e.g. "09:00". Required when IsClosed is false; ignored when true. "24:00" is a valid
    // close time (open until midnight) — the same convention SmartOffers' own stage times use.
    [StringLength(5)]
    public string? OpenTime { get; set; }

    [StringLength(5)]
    public string? CloseTime { get; set; }
}
