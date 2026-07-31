using Eksabli.EntityFrameworkCore;
using Volo.Abp.Autofac;
using Volo.Abp.Modularity;

namespace Eksabli.DbMigrator;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(EksabliEntityFrameworkCoreModule),
    typeof(EksabliApplicationContractsModule)
)]
public class EksabliDbMigratorModule : AbpModule
{
}
