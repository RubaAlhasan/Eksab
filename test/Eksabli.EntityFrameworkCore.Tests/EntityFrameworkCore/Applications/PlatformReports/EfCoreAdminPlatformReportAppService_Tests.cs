using Eksabli.PlatformReports;
using Xunit;

namespace Eksabli.EntityFrameworkCore.Applications.PlatformReports;

[Collection(EksabliTestConsts.CollectionDefinitionName)]
public class EfCoreAdminPlatformReportAppService_Tests : AdminPlatformReportAppService_Tests<EksabliEntityFrameworkCoreTestModule>
{
}
