using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Eksabli.Localization;
using Eksabli.MultiTenancy;
using Eksabli.Notifications;
using Eksabli.Wallets;
using System;
using Volo.Abp;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
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


#if DEBUG
        context.Services.Replace(ServiceDescriptor.Singleton<IEmailSender, NullEmailSender>());
#endif

        ConfigureFcm(context);
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
    }
}
