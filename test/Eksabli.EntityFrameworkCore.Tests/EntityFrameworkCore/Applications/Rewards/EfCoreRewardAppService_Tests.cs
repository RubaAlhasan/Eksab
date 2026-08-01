using Eksabli.Rewards;
using Xunit;

namespace Eksabli.EntityFrameworkCore.Applications.Rewards;

[Collection(EksabliTestConsts.CollectionDefinitionName)]
public class EfCoreRewardAppService_Tests : RewardAppService_Tests<EksabliEntityFrameworkCoreTestModule>
{
}
