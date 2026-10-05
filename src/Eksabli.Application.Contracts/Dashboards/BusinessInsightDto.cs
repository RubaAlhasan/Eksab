using System;

namespace Eksabli.Dashboards;

// A rule that fired on the business's current data. The fields are the facts the rule needs. The UI turns them into a
// sentence, so the wording can be localized without the server knowing the language.
public class BusinessInsightDto
{
    public BusinessInsightKind Kind { get; set; }

    public DashboardInsightSeverity Severity { get; set; }

    // The offer or reward this insight is about, when there is one.
    public Guid? RelatedId { get; set; }

    // The name of that offer, in both languages, for the sentence.
    public string? NameAr { get; set; }

    public string? NameEn { get; set; }

    public int? Count { get; set; }

    // A local hour of day (0 to 23), for the peak-hour insight.
    public int? Hour { get; set; }

    // A percentage, for the coverage insight.
    public decimal? Percent { get; set; }
}
