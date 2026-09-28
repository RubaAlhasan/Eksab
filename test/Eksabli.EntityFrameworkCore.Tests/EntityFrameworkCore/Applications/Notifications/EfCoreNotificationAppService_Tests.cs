using Eksabli.Notifications;
using Xunit;

namespace Eksabli.EntityFrameworkCore.Applications.Notifications;

[Collection(EksabliTestConsts.CollectionDefinitionName)]
public class EfCoreNotificationAppService_Tests : NotificationAppService_Tests<EksabliEntityFrameworkCoreTestModule>
{
}
