using Eksabli.AuditLogs;
using Xunit;

namespace Eksabli.EntityFrameworkCore.Applications.AuditLogs;

[Collection(EksabliTestConsts.CollectionDefinitionName)]
public class EfCoreAdminAuditLogAppService_Tests : AdminAuditLogAppService_Tests<EksabliEntityFrameworkCoreTestModule>
{
}
