using Eksabli.Businesses;
using Xunit;

namespace Eksabli.EntityFrameworkCore.Applications.Businesses;

[Collection(EksabliTestConsts.CollectionDefinitionName)]
public class EfCoreAdminTenantAppService_Tests : AdminTenantAppService_Tests<EksabliEntityFrameworkCoreTestModule>
{
}
