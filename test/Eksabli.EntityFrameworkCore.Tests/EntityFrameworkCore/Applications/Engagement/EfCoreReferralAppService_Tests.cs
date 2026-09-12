using Eksabli.Engagement;
using Xunit;

namespace Eksabli.EntityFrameworkCore.Applications.Engagement;

[Collection(EksabliTestConsts.CollectionDefinitionName)]
public class EfCoreReferralAppService_Tests : ReferralAppService_Tests<EksabliEntityFrameworkCoreTestModule>
{
}
