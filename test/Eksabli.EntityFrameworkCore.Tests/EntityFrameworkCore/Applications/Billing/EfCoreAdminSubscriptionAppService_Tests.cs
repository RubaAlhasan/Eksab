using Eksabli.Billing;
using Xunit;

namespace Eksabli.EntityFrameworkCore.Applications.Billing;

[Collection(EksabliTestConsts.CollectionDefinitionName)]
public class EfCoreAdminSubscriptionAppService_Tests : AdminSubscriptionAppService_Tests<EksabliEntityFrameworkCoreTestModule>
{
}
