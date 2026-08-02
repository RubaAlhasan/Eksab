using Eksabli.Engagement;
using Xunit;

namespace Eksabli.EntityFrameworkCore.Applications.Engagement;

[Collection(EksabliTestConsts.CollectionDefinitionName)]
public class EfCoreAchievementAppService_Tests : AchievementAppService_Tests<EksabliEntityFrameworkCoreTestModule>
{
}
