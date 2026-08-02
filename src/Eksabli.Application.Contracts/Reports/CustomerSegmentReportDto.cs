namespace Eksabli.Reports;

// Segment definitions:
//   New      - Membership.JoinedAt within the trailing 30 days
//   Active   - >=1 Earn PointsTransaction in the trailing 30 days
//   AtRisk   - no Earn in the trailing 30 days, but had one in the 31-90 day window
//   Churned  - no Earn transaction at all in the trailing 90 days, and not New
// A membership can only land in exactly one bucket — New takes priority over Active/AtRisk/Churned.
public class CustomerSegmentReportDto
{
    public int New { get; set; }

    public int Active { get; set; }

    public int AtRisk { get; set; }

    public int Churned { get; set; }
}
