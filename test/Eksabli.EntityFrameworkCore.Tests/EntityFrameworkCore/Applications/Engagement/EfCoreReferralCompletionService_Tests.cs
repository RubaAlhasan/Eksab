using Eksabli.Engagement;
using Xunit;

namespace Eksabli.EntityFrameworkCore.Applications.Engagement;

[Collection(EksabliTestConsts.CollectionDefinitionName)]
public class EfCoreReferralCompletionService_Tests : ReferralCompletionService_Tests<EksabliEntityFrameworkCoreTestModule>
{
}
