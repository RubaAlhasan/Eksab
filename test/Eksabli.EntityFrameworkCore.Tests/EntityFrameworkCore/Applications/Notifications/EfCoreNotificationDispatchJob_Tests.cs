using Eksabli.Notifications;
using Xunit;

namespace Eksabli.EntityFrameworkCore.Applications.Notifications;

[Collection(EksabliTestConsts.CollectionDefinitionName)]
public class EfCoreNotificationDispatchJob_Tests : NotificationDispatchJob_Tests<EksabliEntityFrameworkCoreTestModule>
{
}
