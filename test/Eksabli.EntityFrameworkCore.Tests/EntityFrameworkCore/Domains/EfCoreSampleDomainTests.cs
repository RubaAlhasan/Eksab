using Eksabli.Samples;
using Xunit;

namespace Eksabli.EntityFrameworkCore.Domains;

[Collection(EksabliTestConsts.CollectionDefinitionName)]
public class EfCoreSampleDomainTests : SampleDomainTests<EksabliEntityFrameworkCoreTestModule>
{

}
