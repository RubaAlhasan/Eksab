using Eksabli.Reviews;
using Xunit;

namespace Eksabli.EntityFrameworkCore.Applications.Reviews;

[Collection(EksabliTestConsts.CollectionDefinitionName)]
public class EfCoreCustomerReviewAppService_Tests : CustomerReviewAppService_Tests<EksabliEntityFrameworkCoreTestModule>
{
}
