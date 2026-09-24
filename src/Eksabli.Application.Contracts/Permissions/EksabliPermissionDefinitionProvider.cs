using Eksabli.Localization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Localization;
using Volo.Abp.MultiTenancy;

namespace Eksabli.Permissions;

public class EksabliPermissionDefinitionProvider : PermissionDefinitionProvider
{
    public override void Define(IPermissionDefinitionContext context)
    {
        var myGroup = context.AddGroup(EksabliPermissions.GroupName);

        // Everything down to (but not including) Billing is Business-Portal-only — a tenant's own
        // data/actions, never something a Host-realm role has any use for. Explicitly restricted via
        // MultiTenancySides.Tenant for the same reason the Host-only block further down is restricted
        // to MultiTenancySides.Host (see that block's own comment): without this, ABP's "grant all
        // currently-defined permissions" seeding would also offer these to a Host role, and — more
        // importantly, once a Business Portal Roles/Permissions page exists — a tenant's own Owner
        // would see every OTHER tenant's/platform's permissions in the same tree as their own.
        var businessProfilePermission = myGroup.AddPermission(EksabliPermissions.BusinessProfile.Default, L("Permission:BusinessProfile"), MultiTenancySides.Tenant);
        businessProfilePermission.AddChild(EksabliPermissions.BusinessProfile.Edit, L("Permission:BusinessProfile.Edit"), MultiTenancySides.Tenant);

        var branchesPermission = myGroup.AddPermission(EksabliPermissions.Branches.Default, L("Permission:Branches"), MultiTenancySides.Tenant);
        branchesPermission.AddChild(EksabliPermissions.Branches.Create, L("Permission:Branches.Create"), MultiTenancySides.Tenant);
        branchesPermission.AddChild(EksabliPermissions.Branches.Edit, L("Permission:Branches.Edit"), MultiTenancySides.Tenant);
        branchesPermission.AddChild(EksabliPermissions.Branches.Delete, L("Permission:Branches.Delete"), MultiTenancySides.Tenant);

        var employeeAssignmentsPermission = myGroup.AddPermission(EksabliPermissions.EmployeeAssignments.Default, L("Permission:EmployeeAssignments"), MultiTenancySides.Tenant);
        employeeAssignmentsPermission.AddChild(EksabliPermissions.EmployeeAssignments.Create, L("Permission:EmployeeAssignments.Create"), MultiTenancySides.Tenant);
        employeeAssignmentsPermission.AddChild(EksabliPermissions.EmployeeAssignments.Edit, L("Permission:EmployeeAssignments.Edit"), MultiTenancySides.Tenant);
        employeeAssignmentsPermission.AddChild(EksabliPermissions.EmployeeAssignments.Delete, L("Permission:EmployeeAssignments.Delete"), MultiTenancySides.Tenant);

        var membershipsPermission = myGroup.AddPermission(EksabliPermissions.Memberships.Default, L("Permission:Memberships"), MultiTenancySides.Tenant);
        membershipsPermission.AddChild(EksabliPermissions.Memberships.View, L("Permission:Memberships.View"), MultiTenancySides.Tenant);
        membershipsPermission.AddChild(EksabliPermissions.Memberships.Award, L("Permission:Memberships.Award"), MultiTenancySides.Tenant);
        membershipsPermission.AddChild(EksabliPermissions.Memberships.Adjust, L("Permission:Memberships.Adjust"), MultiTenancySides.Tenant);
        membershipsPermission.AddChild(EksabliPermissions.Memberships.Edit, L("Permission:Memberships.Edit"), MultiTenancySides.Tenant);

        var tiersPermission = myGroup.AddPermission(EksabliPermissions.Tiers.Default, L("Permission:Tiers"), MultiTenancySides.Tenant);
        tiersPermission.AddChild(EksabliPermissions.Tiers.Create, L("Permission:Tiers.Create"), MultiTenancySides.Tenant);
        tiersPermission.AddChild(EksabliPermissions.Tiers.Edit, L("Permission:Tiers.Edit"), MultiTenancySides.Tenant);
        tiersPermission.AddChild(EksabliPermissions.Tiers.Delete, L("Permission:Tiers.Delete"), MultiTenancySides.Tenant);

        var pointRulesPermission = myGroup.AddPermission(EksabliPermissions.PointRules.Default, L("Permission:PointRules"), MultiTenancySides.Tenant);
        pointRulesPermission.AddChild(EksabliPermissions.PointRules.Create, L("Permission:PointRules.Create"), MultiTenancySides.Tenant);
        pointRulesPermission.AddChild(EksabliPermissions.PointRules.Edit, L("Permission:PointRules.Edit"), MultiTenancySides.Tenant);
        pointRulesPermission.AddChild(EksabliPermissions.PointRules.Delete, L("Permission:PointRules.Delete"), MultiTenancySides.Tenant);

        var rewardsPermission = myGroup.AddPermission(EksabliPermissions.Rewards.Default, L("Permission:Rewards"), MultiTenancySides.Tenant);
        rewardsPermission.AddChild(EksabliPermissions.Rewards.Create, L("Permission:Rewards.Create"), MultiTenancySides.Tenant);
        rewardsPermission.AddChild(EksabliPermissions.Rewards.Edit, L("Permission:Rewards.Edit"), MultiTenancySides.Tenant);
        rewardsPermission.AddChild(EksabliPermissions.Rewards.Delete, L("Permission:Rewards.Delete"), MultiTenancySides.Tenant);
        rewardsPermission.AddChild(EksabliPermissions.Rewards.Redeem, L("Permission:Rewards.Redeem"), MultiTenancySides.Tenant);

        // Billing itself has real children on BOTH sides (a tenant Owner's own subscription vs. a
        // platform Billing Admin's cross-tenant view) — left as the default `Both` on the parent, with
        // each child explicitly restricted to the side it actually applies to.
        var billingPermission = myGroup.AddPermission(EksabliPermissions.Billing.Default, L("Permission:Billing"));
        billingPermission.AddChild(EksabliPermissions.Billing.ManageOwn, L("Permission:Billing.ManageOwn"), MultiTenancySides.Tenant);
        billingPermission.AddChild(EksabliPermissions.Billing.ManagePlatform, L("Permission:Billing.ManagePlatform"), MultiTenancySides.Host);

        var campaignsPermission = myGroup.AddPermission(EksabliPermissions.Campaigns.Default, L("Permission:Campaigns"), MultiTenancySides.Tenant);
        campaignsPermission.AddChild(EksabliPermissions.Campaigns.Create, L("Permission:Campaigns.Create"), MultiTenancySides.Tenant);
        campaignsPermission.AddChild(EksabliPermissions.Campaigns.Edit, L("Permission:Campaigns.Edit"), MultiTenancySides.Tenant);
        campaignsPermission.AddChild(EksabliPermissions.Campaigns.Activate, L("Permission:Campaigns.Activate"), MultiTenancySides.Tenant);

        var offersPermission = myGroup.AddPermission(EksabliPermissions.Offers.Default, L("Permission:Offers"), MultiTenancySides.Tenant);
        offersPermission.AddChild(EksabliPermissions.Offers.Create, L("Permission:Offers.Create"), MultiTenancySides.Tenant);
        offersPermission.AddChild(EksabliPermissions.Offers.Edit, L("Permission:Offers.Edit"), MultiTenancySides.Tenant);
        offersPermission.AddChild(EksabliPermissions.Offers.Delete, L("Permission:Offers.Delete"), MultiTenancySides.Tenant);

        // Notifications itself has real children on BOTH sides (a tenant's own campaign-channel send
        // vs. the platform admin's cross-tenant broadcast) — same "Both on the parent, restrict each
        // child" shape as Billing above.
        var notificationsPermission = myGroup.AddPermission(EksabliPermissions.Notifications.Default, L("Permission:Notifications"));
        notificationsPermission.AddChild(EksabliPermissions.Notifications.Send, L("Permission:Notifications.Send"), MultiTenancySides.Tenant);
        notificationsPermission.AddChild(EksabliPermissions.Notifications.Broadcast, L("Permission:Notifications.Broadcast"), MultiTenancySides.Host);

        var achievementsPermission = myGroup.AddPermission(EksabliPermissions.Achievements.Default, L("Permission:Achievements"), MultiTenancySides.Tenant);
        achievementsPermission.AddChild(EksabliPermissions.Achievements.Create, L("Permission:Achievements.Create"), MultiTenancySides.Tenant);
        achievementsPermission.AddChild(EksabliPermissions.Achievements.Edit, L("Permission:Achievements.Edit"), MultiTenancySides.Tenant);
        achievementsPermission.AddChild(EksabliPermissions.Achievements.Delete, L("Permission:Achievements.Delete"), MultiTenancySides.Tenant);
        achievementsPermission.AddChild(EksabliPermissions.Achievements.Award, L("Permission:Achievements.Award"), MultiTenancySides.Tenant);

        var followersPermission = myGroup.AddPermission(EksabliPermissions.Followers.Default, L("Permission:Followers"), MultiTenancySides.Tenant);
        followersPermission.AddChild(EksabliPermissions.Followers.View, L("Permission:Followers.View"), MultiTenancySides.Tenant);
        followersPermission.AddChild(EksabliPermissions.Followers.ConvertToCampaign, L("Permission:Followers.ConvertToCampaign"), MultiTenancySides.Tenant);

        // This tenant's own Business Portal reports/export — distinct from the Host-only
        // PlatformReports block further down.
        var reportsPermission = myGroup.AddPermission(EksabliPermissions.Reports.Default, L("Permission:Reports"), MultiTenancySides.Tenant);
        reportsPermission.AddChild(EksabliPermissions.Reports.Export, L("Permission:Reports.Export"), MultiTenancySides.Tenant);

        // Host-realm only, granted automatically (never through the Roles/Permissions UI) — see
        // EksabliPermissions.Customer's own comment.
        myGroup.AddPermission(EksabliPermissions.Customer.Default, L("Permission:Customer"), MultiTenancySides.Host);

        // Everything from here down is Host-realm-only (platform staff, never a tenant business's own
        // role) — explicitly restricted via MultiTenancySides.Host. Found necessary by LIVE testing
        // this session: without this restriction, ABP's own "Grant all permissions" seeding (applied
        // to a freshly-registered tenant's own Owner role, the exact same seeding path used for the
        // Host "admin" role) genuinely grants these too, since nothing here previously told ABP they
        // were Host-only — a real cross-tenant authorization gap, not just a naming convention. See
        // NEXT_SESSION_PROMPT.md's own gotcha entry for the full writeup and the live-test evidence.
        var tenantsPermission = myGroup.AddPermission(EksabliPermissions.Tenants.Default, L("Permission:Tenants"), MultiTenancySides.Host);
        tenantsPermission.AddChild(EksabliPermissions.Tenants.View, L("Permission:Tenants.View"), MultiTenancySides.Host);
        tenantsPermission.AddChild(EksabliPermissions.Tenants.Approve, L("Permission:Tenants.Approve"), MultiTenancySides.Host);
        tenantsPermission.AddChild(EksabliPermissions.Tenants.Suspend, L("Permission:Tenants.Suspend"), MultiTenancySides.Host);
        tenantsPermission.AddChild(EksabliPermissions.Tenants.Impersonate, L("Permission:Tenants.Impersonate"), MultiTenancySides.Host);

        var usersPermission = myGroup.AddPermission(EksabliPermissions.Users.Default, L("Permission:Users"), MultiTenancySides.Host);
        usersPermission.AddChild(EksabliPermissions.Users.View, L("Permission:Users.View"), MultiTenancySides.Host);

        var categoriesPermission = myGroup.AddPermission(EksabliPermissions.Categories.Default, L("Permission:Categories"), MultiTenancySides.Host);
        categoriesPermission.AddChild(EksabliPermissions.Categories.Create, L("Permission:Categories.Create"), MultiTenancySides.Host);
        categoriesPermission.AddChild(EksabliPermissions.Categories.Edit, L("Permission:Categories.Edit"), MultiTenancySides.Host);
        categoriesPermission.AddChild(EksabliPermissions.Categories.Delete, L("Permission:Categories.Delete"), MultiTenancySides.Host);

        var supportTicketsPermission = myGroup.AddPermission(EksabliPermissions.SupportTickets.Default, L("Permission:SupportTickets"), MultiTenancySides.Host);
        supportTicketsPermission.AddChild(EksabliPermissions.SupportTickets.Manage, L("Permission:SupportTickets.Manage"), MultiTenancySides.Host);

        myGroup.AddPermission(EksabliPermissions.AuditLogs.Default, L("Permission:AuditLogs"), MultiTenancySides.Host);
        myGroup.AddPermission(EksabliPermissions.SmsLogs.Default, L("Permission:SmsLogs"), MultiTenancySides.Host);
        myGroup.AddPermission(EksabliPermissions.PlatformReports.Default, L("Permission:PlatformReports"), MultiTenancySides.Host);
        //Define your own permissions here. Example:
        //myGroup.AddPermission(EksabliPermissions.MyPermission1, L("Permission:MyPermission1"));
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<EksabliResource>(name);
    }
}
