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

    //Add your own permission names. Example:
    //public const string MyPermission1 = GroupName + ".MyPermission1";
}
