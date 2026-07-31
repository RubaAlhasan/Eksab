using Volo.Abp.Modularity;

namespace Eksabli;

[DependsOn(
    typeof(EksabliDomainModule),
    typeof(EksabliTestBaseModule)
)]
public class EksabliDomainTestModule : AbpModule
{

}
