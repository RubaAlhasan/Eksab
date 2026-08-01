using Eksabli.Businesses;
using Xunit;

namespace Eksabli.EntityFrameworkCore.Applications.Businesses;

[Collection(EksabliTestConsts.CollectionDefinitionName)]
public class EfCoreBusinessAppService_Tests : BusinessAppService_Tests<EksabliEntityFrameworkCoreTestModule>
{
}
