using Eksabli.Billing;
using Xunit;

namespace Eksabli.EntityFrameworkCore.Applications.Billing;

[Collection(EksabliTestConsts.CollectionDefinitionName)]
public class EfCoreSubscriptionPlanAppService_Tests : SubscriptionPlanAppService_Tests<EksabliEntityFrameworkCoreTestModule>
{
}
