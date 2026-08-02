using Eksabli.Billing;
using Xunit;

namespace Eksabli.EntityFrameworkCore.Applications.Billing;

[Collection(EksabliTestConsts.CollectionDefinitionName)]
public class EfCoreBillingAppService_Tests : BillingAppService_Tests<EksabliEntityFrameworkCoreTestModule>
{
}
