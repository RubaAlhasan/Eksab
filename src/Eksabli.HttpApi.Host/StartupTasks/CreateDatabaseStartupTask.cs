using System;
using System.Threading.Tasks;
using Eksabli.Data.Seeders.Services;
using Eksabli.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Eksabli.StartupTasks;

public class CreateDatabaseStartupTask : IStartupTask
{
    private readonly ILogger<CreateDatabaseStartupTask> _logger;
    private readonly SeedService _seedService;

    public CreateDatabaseStartupTask(ILogger<CreateDatabaseStartupTask> logger, SeedService seedService)
    {
        _logger = logger;
        _seedService = seedService;
    }

    public async Task Execute(IHost host)
    {
        try
        {
            await UpdateDatabase(host);
            await _seedService.Seed();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database migration/seed failed on startup.");
        }
    }

    private async Task UpdateDatabase(IHost host)
    {
        using var serviceScope = host.Services.GetRequiredService<IServiceScopeFactory>().CreateScope();

        using (var ctx = serviceScope.ServiceProvider.GetRequiredService<EksabliDbContext>())
        {
            await ctx.Database.MigrateAsync();
        }
    }
}
