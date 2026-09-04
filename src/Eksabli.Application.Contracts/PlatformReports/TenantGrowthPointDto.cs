namespace Eksabli.PlatformReports;

// One month's worth of new tenant signups (BusinessProfile.CreationTime bucketed by month), Host-scoped
// via IDataFilter.Disable<IMultiTenant>() — same trailing-7-months, zero-filled shape as
// Eksabli.Billing.MrrTrendPointDto, so the Admin Portal's charts read consistently across pages.
public class TenantGrowthPointDto
{
    public int Year { get; set; }

    public int Month { get; set; }

    public int NewTenants { get; set; }
}
