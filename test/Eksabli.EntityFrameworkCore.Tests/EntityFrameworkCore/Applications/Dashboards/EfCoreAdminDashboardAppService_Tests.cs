using Eksabli.Dashboards;
using Xunit;

namespace Eksabli.EntityFrameworkCore.Applications.Dashboards;

[Collection(EksabliTestConsts.CollectionDefinitionName)]
public class EfCoreAdminDashboardAppService_Tests : AdminDashboardAppService_Tests<EksabliEntityFrameworkCoreTestModule>
{
}
