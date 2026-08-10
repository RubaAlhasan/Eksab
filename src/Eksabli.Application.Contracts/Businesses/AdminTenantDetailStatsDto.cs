namespace Eksabli.Businesses;

// Powers the Admin Portal > Business Details profile card's Owner/Branches rows and the Overview tab's
// stat grid (Points Issued/Redeemed, Active Campaigns — Support Tickets is fetched separately via the
// existing SupportTicketsService call, already real). Deliberately NOT merged into AdminTenantDto:
// that DTO is also returned by the paged GetListAsync, and these fields all need a per-tenant
// cross-entity aggregate query each — cheap for one tenant (the detail page), but N+1 if computed for
// every row of a list.
public class AdminTenantDetailStatsDto
{
    // Real IdentityUser.Name + .Surname of the EmployeeAssignment with Role == Owner for this tenant —
    // null if unset (today, always — nothing in this codebase's registration flow ever populates those
    // fields for a business owner, confirmed by reading BusinessAppService.RegisterAsync; same finding
    // already documented on the Coupons/Employees pages for staff generally). The UI prefers this over
    // OwnerEmail when present, so it's correct automatically if a real name is ever set later, rather
    // than hardcoding "always show email" for a limitation that isn't structural.
    public string? OwnerDisplayName { get; set; }

    // Always populated (when an Owner assignment exists) — shown as a hover tooltip regardless of
    // whether OwnerDisplayName is available, and as the fallback text when it isn't.
    public string? OwnerEmail { get; set; }

    public int BranchCount { get; set; }

    public int PointsIssuedLast30Days { get; set; }

    public int PointsRedeemedLast30Days { get; set; }

    public int ActiveCampaignCount { get; set; }
}
