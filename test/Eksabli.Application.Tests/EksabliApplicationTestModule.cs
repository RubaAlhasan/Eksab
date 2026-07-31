using Volo.Abp.Modularity;

namespace Eksabli;

[DependsOn(
    typeof(EksabliApplicationModule),
    typeof(EksabliDomainTestModule)
)]
public class EksabliApplicationTestModule : AbpModule
{

}
