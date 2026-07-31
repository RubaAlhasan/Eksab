using Xunit;

namespace Eksabli.EntityFrameworkCore;

[CollectionDefinition(EksabliTestConsts.CollectionDefinitionName)]
public class EksabliEntityFrameworkCoreCollection : ICollectionFixture<EksabliEntityFrameworkCoreFixture>
{

}
