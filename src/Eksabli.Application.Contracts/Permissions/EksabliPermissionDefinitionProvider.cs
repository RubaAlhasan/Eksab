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

        var booksPermission = myGroup.AddPermission(EksabliPermissions.Books.Default, L("Permission:Books"));
        booksPermission.AddChild(EksabliPermissions.Books.Create, L("Permission:Books.Create"));
        booksPermission.AddChild(EksabliPermissions.Books.Edit, L("Permission:Books.Edit"));
        booksPermission.AddChild(EksabliPermissions.Books.Delete, L("Permission:Books.Delete"));

        var authorsPermission = myGroup.AddPermission(EksabliPermissions.Authors.Default, L("Permission:Authors"));
        authorsPermission.AddChild(EksabliPermissions.Authors.Create, L("Permission:Authors.Create"));
        authorsPermission.AddChild(EksabliPermissions.Authors.Edit, L("Permission:Authors.Edit"));
        authorsPermission.AddChild(EksabliPermissions.Authors.Delete, L("Permission:Authors.Delete"));

        var businessProfilePermission = myGroup.AddPermission(EksabliPermissions.BusinessProfile.Default, L("Permission:BusinessProfile"));
        businessProfilePermission.AddChild(EksabliPermissions.BusinessProfile.Edit, L("Permission:BusinessProfile.Edit"));

        var branchesPermission = myGroup.AddPermission(EksabliPermissions.Branches.Default, L("Permission:Branches"));
        branchesPermission.AddChild(EksabliPermissions.Branches.Create, L("Permission:Branches.Create"));
        branchesPermission.AddChild(EksabliPermissions.Branches.Edit, L("Permission:Branches.Edit"));
        branchesPermission.AddChild(EksabliPermissions.Branches.Delete, L("Permission:Branches.Delete"));

        var employeeAssignmentsPermission = myGroup.AddPermission(EksabliPermissions.EmployeeAssignments.Default, L("Permission:EmployeeAssignments"));
        employeeAssignmentsPermission.AddChild(EksabliPermissions.EmployeeAssignments.Create, L("Permission:EmployeeAssignments.Create"));
        employeeAssignmentsPermission.AddChild(EksabliPermissions.EmployeeAssignments.Edit, L("Permission:EmployeeAssignments.Edit"));
        employeeAssignmentsPermission.AddChild(EksabliPermissions.EmployeeAssignments.Delete, L("Permission:EmployeeAssignments.Delete"));

        var membershipsPermission = myGroup.AddPermission(EksabliPermissions.Memberships.Default, L("Permission:Memberships"));
        membershipsPermission.AddChild(EksabliPermissions.Memberships.Award, L("Permission:Memberships.Award"));
        membershipsPermission.AddChild(EksabliPermissions.Memberships.Adjust, L("Permission:Memberships.Adjust"));

        var tiersPermission = myGroup.AddPermission(EksabliPermissions.Tiers.Default, L("Permission:Tiers"));
        tiersPermission.AddChild(EksabliPermissions.Tiers.Create, L("Permission:Tiers.Create"));
        tiersPermission.AddChild(EksabliPermissions.Tiers.Edit, L("Permission:Tiers.Edit"));
        tiersPermission.AddChild(EksabliPermissions.Tiers.Delete, L("Permission:Tiers.Delete"));

        var pointRulesPermission = myGroup.AddPermission(EksabliPermissions.PointRules.Default, L("Permission:PointRules"));
        pointRulesPermission.AddChild(EksabliPermissions.PointRules.Create, L("Permission:PointRules.Create"));
        pointRulesPermission.AddChild(EksabliPermissions.PointRules.Edit, L("Permission:PointRules.Edit"));
        pointRulesPermission.AddChild(EksabliPermissions.PointRules.Delete, L("Permission:PointRules.Delete"));
        //Define your own permissions here. Example:
        //myGroup.AddPermission(EksabliPermissions.MyPermission1, L("Permission:MyPermission1"));
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<EksabliResource>(name);
    }
}
