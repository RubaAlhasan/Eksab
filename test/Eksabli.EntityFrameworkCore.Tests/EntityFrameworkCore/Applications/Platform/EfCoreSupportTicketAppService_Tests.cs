using Eksabli.Platform;
using Xunit;

namespace Eksabli.EntityFrameworkCore.Applications.Platform;

[Collection(EksabliTestConsts.CollectionDefinitionName)]
public class EfCoreSupportTicketAppService_Tests : SupportTicketAppService_Tests<EksabliEntityFrameworkCoreTestModule>
{
}
