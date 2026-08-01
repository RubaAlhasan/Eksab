using Eksabli.Rewards;
using Xunit;

namespace Eksabli.EntityFrameworkCore.Applications.Rewards;

[Collection(EksabliTestConsts.CollectionDefinitionName)]
public class EfCoreCouponAppService_Tests : CouponAppService_Tests<EksabliEntityFrameworkCoreTestModule>
{
}
