using Eksabli.Rewards;
using Xunit;

namespace Eksabli.EntityFrameworkCore.Applications.Rewards;

[Collection(EksabliTestConsts.CollectionDefinitionName)]
public class EfCoreCouponAuditAppService_Tests : CouponAuditAppService_Tests<EksabliEntityFrameworkCoreTestModule>
{
}
