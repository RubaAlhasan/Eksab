using System;
using Eksabli.Permissions;

namespace Eksabli.EmployeeAssignments;

// Sensible starting point for each employee tier's real ABP Identity Role — the business's own Owner
// can still adjust per-role from the Business Portal's Roles & Permissions page after this (see
// EmployeeAssignmentAppService's own comment for why this exists at all: invited staff previously got
// no ABP role/permission grant whatsoever, leaving every permission-gated endpoint unreachable to
// them). Deliberately NOT an exhaustive/precisely-tuned least-privilege model per tier — "reasonable
// defaults an Owner tweaks from" is the goal. Owner itself isn't listed here — it maps straight to the
// tenant's pre-existing "admin" role (created once, during BusinessAppService.RegisterAsync), never
// re-provisioned or re-seeded by this class.
public static class EmployeeRolePermissionDefaults
{
    public static string RoleName(EmployeeRole role) => role switch
    {
        EmployeeRole.Owner => "admin",
        EmployeeRole.BranchManager => nameof(EmployeeRole.BranchManager),
        EmployeeRole.Cashier => nameof(EmployeeRole.Cashier),
        EmployeeRole.MarketingManager => nameof(EmployeeRole.MarketingManager),
        _ => throw new ArgumentOutOfRangeException(nameof(role)),
    };

    public static string[] DefaultPermissions(EmployeeRole role) => role switch
    {
        EmployeeRole.BranchManager => new[]
        {
            EksabliPermissions.Memberships.Default,
            EksabliPermissions.Memberships.View,
            EksabliPermissions.Memberships.Award,
            EksabliPermissions.Memberships.Adjust,
            EksabliPermissions.Memberships.Edit,
            EksabliPermissions.Branches.Default,
            EksabliPermissions.Branches.Edit,
            EksabliPermissions.Rewards.Default,
            EksabliPermissions.Rewards.Create,
            EksabliPermissions.Rewards.Edit,
            EksabliPermissions.Rewards.Delete,
            EksabliPermissions.Rewards.Redeem,
            EksabliPermissions.Campaigns.Default,
            EksabliPermissions.Campaigns.Create,
            EksabliPermissions.Campaigns.Edit,
            EksabliPermissions.Campaigns.Activate,
            EksabliPermissions.Offers.Default,
            EksabliPermissions.Offers.Create,
            EksabliPermissions.Offers.Edit,
            EksabliPermissions.Offers.Delete,
            EksabliPermissions.Followers.Default,
            EksabliPermissions.Followers.View,
            EksabliPermissions.Reports.Default,
            EksabliPermissions.Reports.Export,
        },
        EmployeeRole.Cashier => new[]
        {
            // Matches what a Cashier can already do via PosAppService.CheckStaffRoleAsync's own
            // EmployeeRole check (Award/Adjust/Redeem-confirm) — just also reachable through ABP's
            // real permission system now, e.g. the Members list, not only raw POS actions.
            EksabliPermissions.Memberships.Default,
            EksabliPermissions.Memberships.View,
            EksabliPermissions.Memberships.Award,
            EksabliPermissions.Memberships.Adjust,
            EksabliPermissions.Rewards.Default,
            EksabliPermissions.Rewards.Redeem,
        },
        EmployeeRole.MarketingManager => new[]
        {
            EksabliPermissions.Campaigns.Default,
            EksabliPermissions.Campaigns.Create,
            EksabliPermissions.Campaigns.Edit,
            EksabliPermissions.Campaigns.Activate,
            EksabliPermissions.Offers.Default,
            EksabliPermissions.Offers.Create,
            EksabliPermissions.Offers.Edit,
            EksabliPermissions.Offers.Delete,
            EksabliPermissions.Notifications.Default,
            EksabliPermissions.Notifications.Send,
            EksabliPermissions.Followers.Default,
            EksabliPermissions.Followers.View,
            EksabliPermissions.Followers.ConvertToCampaign,
            EksabliPermissions.Achievements.Default,
            EksabliPermissions.Achievements.Create,
            EksabliPermissions.Achievements.Edit,
            EksabliPermissions.Achievements.Delete,
            EksabliPermissions.Achievements.Award,
            EksabliPermissions.Reports.Default,
            EksabliPermissions.Reports.Export,
        },
        _ => Array.Empty<string>(), // Owner: relies entirely on the pre-existing "admin" role's own grants.
    };
}
