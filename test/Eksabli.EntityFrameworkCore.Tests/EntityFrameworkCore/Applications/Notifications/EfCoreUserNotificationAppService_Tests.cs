using Eksabli.Notifications;
using Xunit;

namespace Eksabli.EntityFrameworkCore.Applications.Notifications;

[Collection(EksabliTestConsts.CollectionDefinitionName)]
public class EfCoreUserNotificationAppService_Tests : UserNotificationAppService_Tests<EksabliEntityFrameworkCoreTestModule>
{
}
