namespace Eksabli.Permissions;

public static class EksabliPermissions
{
    public const string GroupName = "Eksabli";


    public static class Books
    {
        public const string Default = GroupName + ".Books";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

    public static class Authors
    {
        public const string Default = GroupName + ".Authors";
        public const string Create = Default + ".Create";
        public const string Edit = Default + ".Edit";
        public const string Delete = Default + ".Delete";
    }

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

    //Add your own permission names. Example:
    //public const string MyPermission1 = GroupName + ".MyPermission1";
}
