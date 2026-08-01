using Eksabli.CustomerProfiles;
using Xunit;

namespace Eksabli.EntityFrameworkCore.Applications.CustomerProfiles;

[Collection(EksabliTestConsts.CollectionDefinitionName)]
public class EfCoreCustomerProfileAppService_Tests : CustomerProfileAppService_Tests<EksabliEntityFrameworkCoreTestModule>
{
}
