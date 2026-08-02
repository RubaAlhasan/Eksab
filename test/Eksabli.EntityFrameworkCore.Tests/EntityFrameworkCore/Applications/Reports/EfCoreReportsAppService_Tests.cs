using Eksabli.Reports;
using Xunit;

namespace Eksabli.EntityFrameworkCore.Applications.Reports;

[Collection(EksabliTestConsts.CollectionDefinitionName)]
public class EfCoreReportsAppService_Tests : ReportsAppService_Tests<EksabliEntityFrameworkCoreTestModule>
{
}
