using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Eksabli.SmartOffers;
using Volo.Abp;

namespace Eksabli.Branches;

// Bridges the wire-level DayOpeningHoursDto list (Application.Contracts) and the domain's pure
// BranchDaySchedule/BranchOpeningHours math, and owns the JSON (de)serialization to/from
// Branch.OpeningHoursJson's single string column — no migration needed, the column's shape just
// changed from free text to a serialized schedule.
public static class BranchOpeningHoursMapper
{
    public static string? Serialize(List<DayOpeningHoursDto>? days)
    {
        if (days == null || days.Count == 0) return null;

        Validate(days);
        return JsonSerializer.Serialize(days);
    }

    public static List<DayOpeningHoursDto> Deserialize(string? openingHoursJson)
    {
        if (string.IsNullOrWhiteSpace(openingHoursJson)) return new List<DayOpeningHoursDto>();

        try
        {
            return JsonSerializer.Deserialize<List<DayOpeningHoursDto>>(openingHoursJson) ?? new List<DayOpeningHoursDto>();
        }
        catch (JsonException)
        {
            // Pre-existing free-text data from before this feature, or otherwise malformed — treated as
            // "no structured hours", never a failed request. Same defensive-read convention
            // BusinessAppService.ReadSocialLink already uses for BusinessProfile.SocialLinksJson.
            return new List<DayOpeningHoursDto>();
        }
    }

    public static List<BranchDaySchedule> ToDomain(List<DayOpeningHoursDto> days) =>
        days.Select(d => new BranchDaySchedule(
                d.DayOfWeek,
                d.IsClosed,
                d.IsClosed || !MinuteOfDay.TryParse(d.OpenTime, out var open) ? 0 : open,
                d.IsClosed || !MinuteOfDay.TryParse(d.CloseTime, out var close) ? 0 : close))
            .ToList();

    // The business typed something bad into the hours editor — a UserFriendlyException, not a 500.
    private static void Validate(List<DayOpeningHoursDto> days)
    {
        if (days.Select(d => d.DayOfWeek).Distinct().Count() != days.Count)
        {
            throw new UserFriendlyException("Opening hours can only list each day of the week once.");
        }

        foreach (var day in days)
        {
            if (day.IsClosed) continue;

            if (!MinuteOfDay.TryParse(day.OpenTime, out _) || !MinuteOfDay.TryParse(day.CloseTime, out _))
            {
                throw new UserFriendlyException($"{day.DayOfWeek} needs a valid opening and closing time.");
            }
        }
    }
}
