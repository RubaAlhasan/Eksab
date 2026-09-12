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
    }
}
