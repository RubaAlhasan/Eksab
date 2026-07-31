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
        //Define your own permissions here. Example:
        //myGroup.AddPermission(EksabliPermissions.MyPermission1, L("Permission:MyPermission1"));
    }

    private static LocalizableString L(string name)
    {
        return LocalizableString.Create<EksabliResource>(name);
    }
}
