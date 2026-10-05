using Eksabli.Dashboards;
using Xunit;

namespace Eksabli.EntityFrameworkCore.Applications.Dashboards;

[Collection(EksabliTestConsts.CollectionDefinitionName)]
public class EfCoreBusinessDashboardAppService_Tests : BusinessDashboardAppService_Tests<EksabliEntityFrameworkCoreTestModule>
{
}
