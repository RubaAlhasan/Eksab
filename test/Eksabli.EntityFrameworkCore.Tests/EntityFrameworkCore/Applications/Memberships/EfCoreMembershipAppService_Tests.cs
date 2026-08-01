using Eksabli.Memberships;
using Xunit;

namespace Eksabli.EntityFrameworkCore.Applications.Memberships;

[Collection(EksabliTestConsts.CollectionDefinitionName)]
public class EfCoreMembershipAppService_Tests : MembershipAppService_Tests<EksabliEntityFrameworkCoreTestModule>
{
}
