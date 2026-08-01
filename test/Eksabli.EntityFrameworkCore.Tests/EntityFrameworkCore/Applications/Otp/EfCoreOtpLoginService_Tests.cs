using Eksabli.Otp;
using Xunit;

namespace Eksabli.EntityFrameworkCore.Applications.Otp;

[Collection(EksabliTestConsts.CollectionDefinitionName)]
public class EfCoreOtpLoginService_Tests : OtpLoginService_Tests<EksabliEntityFrameworkCoreTestModule>
{
}
