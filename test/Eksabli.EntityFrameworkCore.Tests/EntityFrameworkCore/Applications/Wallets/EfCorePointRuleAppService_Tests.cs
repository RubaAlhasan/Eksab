using Eksabli.Wallets;
using Xunit;

namespace Eksabli.EntityFrameworkCore.Applications.Wallets;

[Collection(EksabliTestConsts.CollectionDefinitionName)]
public class EfCorePointRuleAppService_Tests : PointRuleAppService_Tests<EksabliEntityFrameworkCoreTestModule>
{
}
