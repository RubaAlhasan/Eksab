namespace Eksabli.Permissions;

public static class EksabliPermissions
{
    public const string GroupName = "Eksabli";

    public static class BusinessProfile
    {
        public const string Default = GroupName + ".BusinessProfile";
        public const string Edit = Default + ".Edit";
    }

    public static class Branches
    {
        public const string Default = GroupName + ".Branches";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class EmployeeAssignments
    {
        public const string Default = GroupName + ".EmployeeAssignments";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class Memberships
    {
        public const string Default = GroupName + ".Memberships";
        // Business Portal > Customers page — the member list itself, same shape as Followers.View.
        public const string View = Default + ".View";
        // Defined for parity/future use — Award/Adjust access is enforced via an EmployeeAssignment.Role
        // check inside PosAppService, not via these permissions (see PosAppService.CheckStaffRoleAsync).
        public const string Award = Default + ".Award";
        public const string Adjust = Default + ".Adjust";
    }

    public static class Tiers
    {
        public const string Default = GroupName + ".Tiers";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class PointRules
    {
        public const string Default = GroupName + ".PointRules";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class Rewards
    {
        public const string Default = GroupName + ".Rewards";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
        // Defined for parity/future use — actual redemption-confirm gate is the role check inside
        // PosAppService.ConfirmRedemptionAsync, same treatment as Memberships.Award/.Adjust.
        public const string Redeem = Default + ".Redeem";
    }

    public static class Billing
    {
        public const string Default = GroupName + ".Billing";
        public const string ManageOwn = Default + ".ManageOwn";           // tenant Owner: their own subscription
        public const string ManagePlatform = Default + ".ManagePlatform"; // platform Billing Admin: everything
    }

    public static class Campaigns
    {
        public const string Default = GroupName + ".Campaigns";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";   // also gates Delete — no separate Delete permission
        public const string Activate = Default + ".Activate";
    }

    public static class Offers
    {
        public const string Default = GroupName + ".Offers";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class Notifications
    {
        public const string Default = GroupName + ".Notifications";
        public const string Send = Default + ".Send";
    }

    public static class Achievements
    {
        public const string Default = GroupName + ".Achievements";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
        public const string Award = Default + ".Award";
    }

    public static class Followers
    {
        public const string Default = GroupName + ".Followers";
        public const string View = Default + ".View";
        // Defined for parity/future use — "convert followers to a campaign target" isn't built yet,
        // same treatment as Memberships.Award/.Adjust.
        public const string ConvertToCampaign = Default + ".ConvertToCampaign";
    }

    public static class Reports
    {
        public const string Default = GroupName + ".Reports";
        public const string Export = Default + ".Export";
    }

    // Host-realm platform-operations permissions (Feature 08 — Admin Panel). Distinct from the
    // tenant-realm groups above: these gate Super Admin/Support Agent/Billing Admin/Content Moderator
    // tooling, not anything a business's own staff can be granted.
    public static class Tenants
    {
        public const string Default = GroupName + ".Tenants";
        public const string View = Default + ".View";
        public const string Approve = Default + ".Approve";
        public const string Suspend = Default + ".Suspend";
    }

    // Cross-tenant "Users" directory (Admin Portal) — deliberately its own permission, not reused as
    // Tenants.View, so it can be granted/audited independently (it exposes customer phone numbers and
    // business-staff emails across every tenant, a more sensitive scope than the Businesses list).
    public static class Users
    {
        public const string Default = GroupName + ".Users";
        public const string View = Default + ".View";
    }

    public static class Categories
    {
        public const string Default = GroupName + ".Categories";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class SupportTickets
    {
        public const string Default = GroupName + ".SupportTickets";
        // Reporter-initiated create/read-own doesn't need a permission beyond authenticated — this
        // gates the Support Agent queue/thread/resolve tooling specifically.
        public const string Manage = Default + ".Manage";
    }

    // Platform-wide request audit trail (ABP's own IAuditLogRepository, already recording every
    // request via the OSS Volo.Abp.AuditLogging.Domain/EntityFrameworkCore modules — only the
    // browsable UI layer was missing; that's a paid ABP Commercial feature, so this is a small
    // hand-written query surface over the same real data instead). Host-realm only, same class of
    // sensitive cross-tenant visibility as Users.View.
    public static class AuditLogs
    {
        public const string Default = GroupName + ".AuditLogs";
    }

    //Add your own permission names. Example:
    //public const string MyPermission1 = GroupName + ".MyPermission1";
}
