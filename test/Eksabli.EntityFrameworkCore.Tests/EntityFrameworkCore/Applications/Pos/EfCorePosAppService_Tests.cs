using Eksabli.Pos;
using Xunit;

namespace Eksabli.EntityFrameworkCore.Applications.Pos;

[Collection(EksabliTestConsts.CollectionDefinitionName)]
public class EfCorePosAppService_Tests : PosAppService_Tests<EksabliEntityFrameworkCoreTestModule>
{
}
