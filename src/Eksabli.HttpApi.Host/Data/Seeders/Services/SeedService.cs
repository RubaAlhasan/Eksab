using System.Threading.Tasks;
using Eksabli.Billing;
using Eksabli.Data.Seeders;
using Eksabli.OpenIddict;
using Microsoft.Extensions.DependencyInjection;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Identity;
using Volo.Abp.Uow;

namespace Eksabli.Data.Seeders.Services;

// Runs each seed contributor explicitly on Host app startup (see CreateDatabaseStartupTask), so a
// fresh database is fully usable — including the Identity "admin" login — from `dotnet run` on the
// Host alone, without needing to run Eksabli.DbMigrator first. This is independent of the
// DbMigrator's own IDataSeeder-driven seed pass (EksabliDbMigrationService.SeedDataAsync); running
// both is safe since every contributor guards on "does data already exist?" before inserting. Add
// a new line here whenever a new IDataSeedContributor is introduced and should also run on Host
// startup.
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

        var identityDataSeedContributor = scope.ServiceProvider.GetRequiredService<IdentityDataSeedContributor>();
        await identityDataSeedContributor.SeedAsync(
            new DataSeedContext()
                .WithProperty(IdentityDataSeedContributor.AdminEmailPropertyName, EksabliConsts.AdminEmailDefaultValue)
                .WithProperty(IdentityDataSeedContributor.AdminPasswordPropertyName, EksabliConsts.AdminPasswordDefaultValue));

        // Must run after IdentityDataSeedContributor above — it grants permissions to the "admin"
        // role that step just created/confirmed exists.
        var adminPermissionDataSeederContributor = scope.ServiceProvider.GetRequiredService<AdminPermissionDataSeederContributor>();
        await adminPermissionDataSeederContributor.SeedAsync(new DataSeedContext());

        // See this contributor's own file comment: stock self-registration bypasses CustomerProfile
        // creation entirely, orphaning the account (name/etc. can never show anywhere in this app).
        // OTP is the only real customer-registration path; this closes the other one.
        var selfRegistrationDisabledDataSeederContributor = scope.ServiceProvider.GetRequiredService<SelfRegistrationDisabledDataSeederContributor>();
        await selfRegistrationDisabledDataSeederContributor.SeedAsync(new DataSeedContext());

        var openIddictDataSeedContributor = scope.ServiceProvider.GetRequiredService<OpenIddictDataSeedContributor>();
        await openIddictDataSeedContributor.SeedAsync(new DataSeedContext());

        var subscriptionPlanDataSeederContributor = scope.ServiceProvider.GetRequiredService<SubscriptionPlanDataSeederContributor>();
        await subscriptionPlanDataSeederContributor.SeedAsync(new DataSeedContext());

        var categoryDataSeederContributor = scope.ServiceProvider.GetRequiredService<CategoryDataSeederContributor>();
        await categoryDataSeederContributor.SeedAsync();

        // Must run after SubscriptionPlan above — RegisterAsync provisions a trial subscription and
        // requires a plan flagged IsTrialDefault to already exist.
        var demoBusinessDataSeederContributor = scope.ServiceProvider.GetRequiredService<DemoBusinessDataSeederContributor>();
        await demoBusinessDataSeederContributor.SeedAsync(new DataSeedContext());
    }
}
