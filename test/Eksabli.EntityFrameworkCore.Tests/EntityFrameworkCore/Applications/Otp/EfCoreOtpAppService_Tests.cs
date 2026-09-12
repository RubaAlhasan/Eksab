using Eksabli.Otp;
using Xunit;

namespace Eksabli.EntityFrameworkCore.Applications.Otp;

[Collection(EksabliTestConsts.CollectionDefinitionName)]
public class EfCoreOtpAppService_Tests : OtpAppService_Tests<EksabliEntityFrameworkCoreTestModule>
{
}
