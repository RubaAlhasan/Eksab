using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace Eksabli.Data;

/* This is used if database provider does't define
 * IEksabliDbSchemaMigrator implementation.
 */
public class NullEksabliDbSchemaMigrator : IEksabliDbSchemaMigrator, ITransientDependency
{
    public Task MigrateAsync()
    {
        return Task.CompletedTask;
    }
}
