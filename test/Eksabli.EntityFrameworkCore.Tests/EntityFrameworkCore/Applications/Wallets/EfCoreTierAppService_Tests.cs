using Eksabli.Wallets;
using Xunit;

namespace Eksabli.EntityFrameworkCore.Applications.Wallets;

[Collection(EksabliTestConsts.CollectionDefinitionName)]
public class EfCoreTierAppService_Tests : TierAppService_Tests<EksabliEntityFrameworkCoreTestModule>
{
}
