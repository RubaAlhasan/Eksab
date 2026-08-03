using System.Threading.Tasks;
using Eksabli.OpenIddict;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Uow;

namespace Eksabli.Data.Seeders.Services;

public class SeedService : ISingletonDependency
{
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public SeedService(IServiceScopeFactory serviceScopeFactory)
    {
        _serviceScopeFactory = serviceScopeFactory;
    }

    [UnitOfWork]
    public virtual async Task Seed()
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var openIddictDataSeedContributor = scope.ServiceProvider.GetRequiredService<OpenIddictDataSeedContributor>();
        await openIddictDataSeedContributor.SeedAsync(new DataSeedContext());
    }
}
