using Eksabli.Branches;
using Xunit;

namespace Eksabli.EntityFrameworkCore.Applications.Branches;

[Collection(EksabliTestConsts.CollectionDefinitionName)]
public class EfCoreBranchAppService_Tests : BranchAppService_Tests<EksabliEntityFrameworkCoreTestModule>
{
}
