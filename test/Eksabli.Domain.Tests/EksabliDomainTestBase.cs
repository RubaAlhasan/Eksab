using Volo.Abp.Modularity;

namespace Eksabli;

/* Inherit from this class for your domain layer tests. */
public abstract class EksabliDomainTestBase<TStartupModule> : EksabliTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{

}
