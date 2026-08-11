using Eksabli.Permissions;
using Xunit;

namespace Eksabli.EntityFrameworkCore.Applications.Permissions;

[Collection(EksabliTestConsts.CollectionDefinitionName)]
public class EfCoreEksabliPermissionDefinition_Tests : EksabliPermissionDefinition_Tests<EksabliEntityFrameworkCoreTestModule>
{
}
