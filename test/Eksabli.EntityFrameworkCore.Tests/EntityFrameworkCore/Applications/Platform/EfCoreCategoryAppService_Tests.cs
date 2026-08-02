using Eksabli.Platform;
using Xunit;

namespace Eksabli.EntityFrameworkCore.Applications.Platform;

[Collection(EksabliTestConsts.CollectionDefinitionName)]
public class EfCoreCategoryAppService_Tests : CategoryAppService_Tests<EksabliEntityFrameworkCoreTestModule>
{
}
