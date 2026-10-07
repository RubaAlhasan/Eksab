using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Eksabli.BusinessProfiles;
using Eksabli.Localization;
using Eksabli.MultiTenancy;
using Eksabli.Notifications;
using Eksabli.Wallets;
using System;
using Volo.Abp;
using Volo.Abp.BlobStoring;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Timing;
using Volo.Abp.PermissionManagement.Identity;
using Volo.Abp.SettingManagement;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.BlobStoring.Database;
using Volo.Abp.Caching;
using Volo.Abp.OpenIddict;
using Volo.Abp.PermissionManagement.OpenIddict;
using Volo.Abp.AuditLogging;
using Volo.Abp.BackgroundJobs;
using Volo.Abp.Emailing;
using Volo.Abp.FeatureManagement;
using Volo.Abp.Identity;
using Volo.Abp.TenantManagement;
using Volo.Abp.Data;
using Volo.Abp.PermissionManagement;

namespace Eksabli;

[DependsOn(
    typeof(EksabliDomainSharedModule),
    typeof(AbpAuditLoggingDomainModule),
    typeof(AbpCachingModule),
    typeof(AbpBackgroundJobsDomainModule),
    typeof(AbpBackgroundWorkersModule),
    typeof(AbpFeatureManagementDomainModule),
    typeof(AbpPermissionManagementDomainIdentityModule),
    typeof(AbpPermissionManagementDomainOpenIddictModule),
    typeof(AbpSettingManagementDomainModule),
    typeof(AbpEmailingModule),
    typeof(AbpIdentityDomainModule),
    typeof(AbpOpenIddictDomainModule),
    typeof(AbpTenantManagementDomainModule),
    typeof(BlobStoringDatabaseDomainModule)
    )]
public class EksabliDomainModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpMultiTenancyOptions>(options =>
        {
            options.IsEnabled = MultiTenancyConsts.IsEnabled;
        });

        // Every DateTime this app stores or compares is UTC.
        //
        // ABP defaults Kind to Unspecified, which makes IClock.Now return DateTime.Now — the SERVER'S
        // LOCAL time — written into `timestamp without time zone` columns that carry no offset. That
        // is only ever correct while the server and every client share one timezone, and it silently
        // stops being correct the moment either moves: a reservation window, a campaign end date or a
        // coupon expiry read hours out, with nothing in the data to say so. The customer app's own
        // users are at +04 against a +03 server today.
        //
        // Existing rows were written in local time and are shifted to UTC by the
        // ConvertStoredTimestampsToUtc migration, which pairs with this setting — neither is correct
        // without the other.
        Configure<AbpClockOptions>(options =>
        {
            options.Kind = DateTimeKind.Utc;
        });


#if DEBUG
        context.Services.Replace(ServiceDescriptor.Singleton<IEmailSender, NullEmailSender>());
#endif

        ConfigureFcm(context);
        ConfigureBlobStoring();
        DisableFrameworkPermissionDataSeedContributor();
        ConfigureBackgroundJobs();
    }

    // Unlike background WORKERS (registered below in OnApplicationInitializationAsync via
    // AddBackgroundWorkerAsync), ABP does NOT auto-discover per-item queue JOBS by conventional DI
    // registration — a job type has to be explicitly mapped here or the job executer has nothing to
    // resolve the stored job name back to at dequeue time. Without this, every NotificationDispatchJob
    // enqueued by CampaignSweepWorker/ReferralCompletionService/NotificationAppService.SendAsync
    // (Business Portal's own "Compose" send) inserted fine but then failed at execution with "Undefined
    // background job for the job name: Eksabli.Notifications.NotificationDispatchArgs" and sat
    // abandoned forever at Status=Queued — confirmed live against AbpBackgroundJobs (IsAbandoned=true)
    // and the host's own error log.
    private void ConfigureBackgroundJobs()
    {
        Configure<AbpBackgroundJobOptions>(options =>
        {
            options.AddJob<NotificationDispatchJob>();
        });
    }

    // Volo.Abp.PermissionManagement.PermissionDataSeedContributor is a framework-registered
    // IDataSeedContributor, listed in AbpDataSeedOptions.Contributors and run by every generic
    // IDataSeeder.SeedAsync(...) call — including BusinessAppService.RegisterAsync's own
    // _dataSeeder.SeedAsync(new DataSeedContext(tenant.Id)) and EksabliDbMigrationService's host pass.
    // (DataSeeder.SeedAsync resolves contributors from this options list, NOT by asking DI for every
    // registered IDataSeedContributor, so removing it from DI directly — e.g. via
    // context.Services.RemoveAll/Remove — has no effect; this options list is the actual hook.)
    //
    // It grants every currently-registered, role-compatible, multitenancy-matching permission to the
    // "admin" role — the exact same "grant everything" job AdminPermissionDataSeederContributor (host)
    // and BusinessAppService.GrantOwnerRolePermissionsAsync (tenant) already do — but via
    // IPermissionDataSeeder.SeedAsync's bulk-insert API, which is the SAME buggy bulk API this app's
    // own code moved away from after it reproducibly threw AbpPermissionGrants' unique-constraint
    // violation (see AdminPermissionDataSeederContributor's own header comment). Confirmed live: this
    // framework contributor is what was actually still colliding on every business registration — a
    // multi-row bulk INSERT of ~64 permissions for the new tenant's "admin" role, containing a
    // duplicate within the same batch — even after every app-level seed path had already switched to
    // the one-call-per-permission IPermissionManager.SetForRoleAsync.
    private void DisableFrameworkPermissionDataSeedContributor()
    {
        Configure<AbpDataSeedOptions>(options =>
        {
            options.Contributors.Remove(typeof(PermissionDataSeedContributor));
        });
    }

    // Only consumer of BlobStoringDatabaseDomainModule so far — that dependency (and the DatabaseBlob/
    // DatabaseBlobContainer tables it already migrated) has existed since the very first migration, but
    // nothing in this codebase actually used IBlobContainer<T> until the business logo upload feature.
    private void ConfigureBlobStoring()
    {
        Configure<AbpBlobStoringOptions>(options =>
        {
            options.Containers.Configure<BusinessLogoContainer>(container =>
            {
                container.UseDatabase();
            });
        });
    }

    // Swaps NullPushNotificationSender for the real FCM sender once a provider is actually configured —
    // same "no real provider chosen yet" placeholder pattern as NullSmsSender/NullPaymentGateway, now
    // resolved for push specifically. Leave Fcm:CredentialsFilePath unset in dev to keep using the Null
    // sender (logs instead of calling out to Firebase).
    private void ConfigureFcm(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();
        Configure<FcmOptions>(configuration.GetSection("Fcm"));

        if (!string.IsNullOrWhiteSpace(configuration["Fcm:CredentialsFilePath"]))
        {
            context.Services.Replace(ServiceDescriptor.Singleton<IPushNotificationSender, FirebaseCloudMessagingSender>());
        }
    }

    public override async Task OnApplicationInitializationAsync(ApplicationInitializationContext context)
    {
        await context.AddBackgroundWorkerAsync<PointsExpirationWorker>();
        await context.AddBackgroundWorkerAsync<Billing.SubscriptionRenewalWorker>();
        await context.AddBackgroundWorkerAsync<Campaigns.CampaignSweepWorker>();
        await context.AddBackgroundWorkerAsync<Rewards.RedemptionReservationWorker>();
        await context.AddBackgroundWorkerAsync<SmartOffers.SmartOfferOrderExpirationWorker>();
        await context.AddBackgroundWorkerAsync<SmartOffers.SmartOfferPriceWatchWorker>();
    }
}
