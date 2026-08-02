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

        var rewardsPermission = myGroup.AddPermission(EksabliPermissions.Rewards.Default, L("Permission:Rewards"));
        rewardsPermission.AddChild(EksabliPermissions.Rewards.Create, L("Permission:Rewards.Create"));
        rewardsPermission.AddChild(EksabliPermissions.Rewards.Edit, L("Permission:Rewards.Edit"));
        rewardsPermission.AddChild(EksabliPermissions.Rewards.Delete, L("Permission:Rewards.Delete"));
        rewardsPermission.AddChild(EksabliPermissions.Rewards.Redeem, L("Permission:Rewards.Redeem"));

        var billingPermission = myGroup.AddPermission(EksabliPermissions.Billing.Default, L("Permission:Billing"));
        billingPermission.AddChild(EksabliPermissions.Billing.ManageOwn, L("Permission:Billing.ManageOwn"));
        billingPermission.AddChild(EksabliPermissions.Billing.ManagePlatform, L("Permission:Billing.ManagePlatform"));

        var campaignsPermission = myGroup.AddPermission(EksabliPermissions.Campaigns.Default, L("Permission:Campaigns"));
        campaignsPermission.AddChild(EksabliPermissions.Campaigns.Create, L("Permission:Campaigns.Create"));
        campaignsPermission.AddChild(EksabliPermissions.Campaigns.Edit, L("Permission:Campaigns.Edit"));
        campaignsPermission.AddChild(EksabliPermissions.Campaigns.Activate, L("Permission:Campaigns.Activate"));

        var offersPermission = myGroup.AddPermission(EksabliPermissions.Offers.Default, L("Permission:Offers"));
        offersPermission.AddChild(EksabliPermissions.Offers.Create, L("Permission:Offers.Create"));
        offersPermission.AddChild(EksabliPermissions.Offers.Edit, L("Permission:Offers.Edit"));
        offersPermission.AddChild(EksabliPermissions.Offers.Delete, L("Permission:Offers.Delete"));

        var notificationsPermission = myGroup.AddPermission(EksabliPermissions.Notifications.Default, L("Permission:Notifications"));
        notificationsPermission.AddChild(EksabliPermissions.Notifications.Send, L("Permission:Notifications.Send"));

        var achievementsPermission = myGroup.AddPermission(EksabliPermissions.Achievements.Default, L("Permission:Achievements"));
        achievementsPermission.AddChild(EksabliPermissions.Achievements.Create, L("Permission:Achievements.Create"));
        achievementsPermission.AddChild(EksabliPermissions.Achievements.Edit, L("Permission:Achievements.Edit"));
        achievementsPermission.AddChild(EksabliPermissions.Achievements.Delete, L("Permission:Achievements.Delete"));
        achievementsPermission.AddChild(EksabliPermissions.Achievements.Award, L("Permission:Achievements.Award"));

        var followersPermission = myGroup.AddPermission(EksabliPermissions.Followers.Default, L("Permission:Followers"));
        followersPermission.AddChild(EksabliPermissions.Followers.View, L("Permission:Followers.View"));
        followersPermission.AddChild(EksabliPermissions.Followers.ConvertToCampaign, L("Permission:Followers.ConvertToCampaign"));

        var reportsPermission = myGroup.AddPermission(EksabliPermissions.Reports.Default, L("Permission:Reports"));
        reportsPermission.AddChild(EksabliPermissions.Reports.Export, L("Permission:Reports.Export"));
        //Define your own permissions here. Example:
        //myGroup.AddPermission(EksabliPermissions.MyPermission1, L("Permission:MyPermission1"));
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<EksabliResource>(name);
    }
}
