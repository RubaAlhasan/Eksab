using Volo.Abp.Modularity;

namespace Eksabli;

public abstract class EksabliApplicationTestBase<TStartupModule> : EksabliTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{

}
