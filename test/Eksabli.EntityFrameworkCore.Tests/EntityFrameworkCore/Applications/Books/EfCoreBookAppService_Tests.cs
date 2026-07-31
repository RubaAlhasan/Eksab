using Eksabli.Books;
using Xunit;

namespace Eksabli.EntityFrameworkCore.Applications.Books;

[Collection(EksabliTestConsts.CollectionDefinitionName)]
public class EfCoreBookAppService_Tests : BookAppService_Tests<EksabliEntityFrameworkCoreTestModule>
{

}