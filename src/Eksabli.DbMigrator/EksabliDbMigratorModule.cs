using Eksabli.EntityFrameworkCore;
using Volo.Abp.Autofac;
using Volo.Abp.Modularity;

namespace Eksabli.DbMigrator;

// Depends on the full EksabliApplicationModule (not just Application.Contracts) so seed
// contributors here can call real application services — e.g. DemoBusinessDataSeederContributor
// calling IBusinessAppService.RegisterAsync to provision a demo business+branch the same way a
// real signup would, rather than duplicating that orchestration logic in the Domain layer.
[DependsOn(
    typeof(AbpAutofacModule),
    typeof(EksabliEntityFrameworkCoreModule),
    typeof(EksabliApplicationModule)
)]
public class EksabliDbMigratorModule : AbpModule
{
}
