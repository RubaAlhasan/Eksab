using Eksabli.Devices;
using Xunit;

namespace Eksabli.EntityFrameworkCore.Applications.Devices;

[Collection(EksabliTestConsts.CollectionDefinitionName)]
public class EfCoreDeviceAppService_Tests : DeviceAppService_Tests<EksabliEntityFrameworkCoreTestModule>
{
}
