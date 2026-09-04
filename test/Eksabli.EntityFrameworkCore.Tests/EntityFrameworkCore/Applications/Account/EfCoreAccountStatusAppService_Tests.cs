using Eksabli.Account;
using Xunit;

namespace Eksabli.EntityFrameworkCore.Applications.Account;

[Collection(EksabliTestConsts.CollectionDefinitionName)]
public class EfCoreAccountStatusAppService_Tests : AccountStatusAppService_Tests<EksabliEntityFrameworkCoreTestModule>
{
}
