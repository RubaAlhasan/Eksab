using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Eksabli.Data;
using Volo.Abp.DependencyInjection;

namespace Eksabli.EntityFrameworkCore;

public class EntityFrameworkCoreEksabliDbSchemaMigrator
    : IEksabliDbSchemaMigrator, ITransientDependency
{
    private readonly IServiceProvider _serviceProvider;

    public EntityFrameworkCoreEksabliDbSchemaMigrator(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task MigrateAsync()
    {
        /* We intentionally resolving the EksabliDbContext
         * from IServiceProvider (instead of directly injecting it)
         * to properly get the connection string of the current tenant in the
         * current scope.
         */

        await _serviceProvider
            .GetRequiredService<EksabliDbContext>()
            .Database
            .MigrateAsync();
    }
}
