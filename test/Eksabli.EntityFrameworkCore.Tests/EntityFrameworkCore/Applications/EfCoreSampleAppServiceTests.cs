using Eksabli.Samples;
using Xunit;

namespace Eksabli.EntityFrameworkCore.Applications;

[Collection(EksabliTestConsts.CollectionDefinitionName)]
public class EfCoreSampleAppServiceTests : SampleAppServiceTests<EksabliEntityFrameworkCoreTestModule>
{

}
