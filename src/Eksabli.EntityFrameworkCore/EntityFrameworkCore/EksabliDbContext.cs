using Microsoft.EntityFrameworkCore;
using Volo.Abp.AuditLogging.EntityFrameworkCore;
using Eksabli.Memberships;
using Eksabli.BusinessProfiles;
using Eksabli.Branches;
using Eksabli.CustomerProfiles;
using Eksabli.EmployeeAssignments;
using Eksabli.Devices;
using Eksabli.Wallets;
using Eksabli.Rewards;
using Eksabli.Billing;
using Eksabli.Campaigns;
using Eksabli.Offers;
using Eksabli.Notifications;
using Eksabli.Engagement;
using Eksabli.Platform;
using Eksabli.Sms;
using Volo.Abp.BackgroundJobs.EntityFrameworkCore;
using Volo.Abp.BlobStoring.Database.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;
using Volo.Abp.FeatureManagement.EntityFrameworkCore;
using Volo.Abp.Identity;
using Volo.Abp.Identity.EntityFrameworkCore;
using Volo.Abp.PermissionManagement.EntityFrameworkCore;
using Volo.Abp.SettingManagement.EntityFrameworkCore;
using Volo.Abp.OpenIddict.EntityFrameworkCore;
using Volo.Abp.TenantManagement;
using Volo.Abp.TenantManagement.EntityFrameworkCore;

namespace Eksabli.EntityFrameworkCore;

[ReplaceDbContext(typeof(IIdentityDbContext))]
[ReplaceDbContext(typeof(ITenantManagementDbContext))]
[ConnectionStringName("Default")]
public class EksabliDbContext :
    AbpDbContext<EksabliDbContext>,
    ITenantManagementDbContext,
    IIdentityDbContext
{
    /* Add DbSet properties for your Aggregate Roots / Entities here. */

    public DbSet<Membership> Memberships { get; set; }

    public DbSet<BusinessProfile> BusinessProfiles { get; set; }

    public DbSet<Branch> Branches { get; set; }

    public DbSet<CustomerProfile> CustomerProfiles { get; set; }

    public DbSet<EmployeeAssignment> EmployeeAssignments { get; set; }

    public DbSet<Device> Devices { get; set; }

    public DbSet<PointsWallet> PointsWallets { get; set; }

    public DbSet<PointsTransaction> PointsTransactions { get; set; }

    public DbSet<Tier> Tiers { get; set; }

    public DbSet<PointRule> PointRules { get; set; }

    public DbSet<Reward> Rewards { get; set; }

    public DbSet<Coupon> Coupons { get; set; }

    public DbSet<SubscriptionPlan> SubscriptionPlans { get; set; }

    public DbSet<TenantSubscription> TenantSubscriptions { get; set; }

    public DbSet<Invoice> Invoices { get; set; }

    public DbSet<Payment> Payments { get; set; }

    public DbSet<Campaign> Campaigns { get; set; }

    public DbSet<Offer> Offers { get; set; }

    public DbSet<Notification> Notifications { get; set; }

    public DbSet<NotificationMessage> NotificationMessages { get; set; }

    public DbSet<UserNotification> UserNotifications { get; set; }

    public DbSet<Referral> Referrals { get; set; }

    public DbSet<Achievement> Achievements { get; set; }

    public DbSet<AchievementAward> AchievementAwards { get; set; }

    public DbSet<Follow> Follows { get; set; }

    public DbSet<Category> Categories { get; set; }

    public DbSet<SupportTicket> SupportTickets { get; set; }

    public DbSet<SmsLog> SmsLogs { get; set; }

    #region Entities from the modules

    /* Notice: We only implemented IIdentityProDbContext and ISaasDbContext
     * and replaced them for this DbContext. This allows you to perform JOIN
     * queries for the entities of these modules over the repositories easily. You
     * typically don't need that for other modules. But, if you need, you can
     * implement the DbContext interface of the needed module and use ReplaceDbContext
     * attribute just like IIdentityProDbContext and ISaasDbContext.
     *
     * More info: Replacing a DbContext of a module ensures that the related module
     * uses this DbContext on runtime. Otherwise, it will use its own DbContext class.
     */

    // Identity
    public DbSet<IdentityUser> Users { get; set; }
    public DbSet<IdentityRole> Roles { get; set; }
    public DbSet<IdentityClaimType> ClaimTypes { get; set; }
    public DbSet<OrganizationUnit> OrganizationUnits { get; set; }
    public DbSet<IdentitySecurityLog> SecurityLogs { get; set; }
    public DbSet<IdentityLinkUser> LinkUsers { get; set; }
    public DbSet<IdentityUserDelegation> UserDelegations { get; set; }
    public DbSet<IdentitySession> Sessions { get; set; }

    // Tenant Management
    public DbSet<Tenant> Tenants { get; set; }
    public DbSet<TenantConnectionString> TenantConnectionStrings { get; set; }

    #endregion

    public EksabliDbContext(DbContextOptions<EksabliDbContext> options)
        : base(options)
    {

    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        /* Include modules to your migration db context */

        builder.ConfigurePermissionManagement();
        builder.ConfigureSettingManagement();
        builder.ConfigureBackgroundJobs();
        builder.ConfigureAuditLogging();
        builder.ConfigureFeatureManagement();
        builder.ConfigureIdentity();
        builder.ConfigureOpenIddict();
        builder.ConfigureTenantManagement();
        builder.ConfigureBlobStoring();

        builder.Entity<IdentityUser>(b =>
        {
            // Host-realm customers are identified by phone number (OTP login) — enforce
            // uniqueness explicitly rather than relying on it being a side effect of
            // UserName also being set to the phone number. Filtered to TenantId IS NULL so
            // tenant-realm staff at unrelated businesses can still share a phone number.
            b.HasIndex(x => x.PhoneNumber)
                .IsUnique()
                .HasFilter("\"TenantId\" IS NULL AND \"PhoneNumber\" IS NOT NULL");
        });


        /* Configure your own tables/entities inside here */

        builder.Entity<Membership>(b =>
        {
            b.ToTable(EksabliConsts.DbTablePrefix + "Memberships", EksabliConsts.DbSchema);
            b.ConfigureByConvention(); //auto configure for the base class props
            b.Property(x => x.CustomerId).IsRequired();
            b.HasIndex(x => new { x.CustomerId, x.TenantId }).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.Status });
        });

        builder.Entity<BusinessProfile>(b =>
        {
            b.ToTable(EksabliConsts.DbTablePrefix + "BusinessProfiles", EksabliConsts.DbSchema);
            b.ConfigureByConvention(); //auto configure for the base class props
            b.Property(x => x.LogoBlobName).HasMaxLength(BusinessProfileConsts.MaxLogoBlobNameLength);
            b.Property(x => x.LogoContentType).HasMaxLength(BusinessProfileConsts.MaxLogoContentTypeLength);
            b.Property(x => x.DescriptionAr).HasMaxLength(BusinessProfileConsts.MaxDescriptionLength);
            b.Property(x => x.DescriptionEn).HasMaxLength(BusinessProfileConsts.MaxDescriptionLength);
            b.Property(x => x.Website).HasMaxLength(BusinessProfileConsts.MaxWebsiteLength);
            b.Property(x => x.SocialLinksJson).HasMaxLength(BusinessProfileConsts.MaxSocialLinksJsonLength);
            b.HasIndex(x => x.TenantId).IsUnique();
        });

        builder.Entity<Branch>(b =>
        {
            b.ToTable(EksabliConsts.DbTablePrefix + "Branches", EksabliConsts.DbSchema);
            b.ConfigureByConvention(); //auto configure for the base class props
            b.Property(x => x.Name).IsRequired().HasMaxLength(BranchConsts.MaxNameLength);
            b.Property(x => x.Address).HasMaxLength(BranchConsts.MaxAddressLength);
            b.Property(x => x.Phone).HasMaxLength(BranchConsts.MaxPhoneLength);
            b.Property(x => x.OpeningHoursJson).HasMaxLength(BranchConsts.MaxOpeningHoursJsonLength);
            b.HasIndex(x => x.TenantId);
        });

        builder.Entity<CustomerProfile>(b =>
        {
            b.ToTable(EksabliConsts.DbTablePrefix + "CustomerProfiles", EksabliConsts.DbSchema);
            b.ConfigureByConvention(); //auto configure for the base class props
            b.Property(x => x.UserId).IsRequired();
            b.Property(x => x.FirstName).HasMaxLength(CustomerProfileConsts.MaxFirstNameLength);
            b.Property(x => x.LastName).HasMaxLength(CustomerProfileConsts.MaxLastNameLength);
            b.HasIndex(x => x.UserId).IsUnique();
        });

        builder.Entity<EmployeeAssignment>(b =>
        {
            b.ToTable(EksabliConsts.DbTablePrefix + "EmployeeAssignments", EksabliConsts.DbSchema);
            b.ConfigureByConvention(); //auto configure for the base class props
            b.Property(x => x.UserId).IsRequired();
            b.HasIndex(x => new { x.TenantId, x.UserId });
            b.HasIndex(x => new { x.TenantId, x.BranchId });
        });

        builder.Entity<Device>(b =>
        {
            b.ToTable(EksabliConsts.DbTablePrefix + "Devices", EksabliConsts.DbSchema);
            b.ConfigureByConvention(); //auto configure for the base class props
            b.Property(x => x.CustomerId).IsRequired();
            b.Property(x => x.PushToken).HasMaxLength(DeviceConsts.MaxPushTokenLength);
            b.Property(x => x.AppVersion).HasMaxLength(DeviceConsts.MaxAppVersionLength);
            b.HasIndex(x => x.CustomerId);
            b.HasIndex(x => x.PushToken).IsUnique();
        });

        builder.Entity<PointsWallet>(b =>
        {
            b.ToTable(EksabliConsts.DbTablePrefix + "PointsWallets", EksabliConsts.DbSchema);
            b.ConfigureByConvention(); //auto configure for the base class props
            b.Property(x => x.MembershipId).IsRequired();
            b.HasIndex(x => x.MembershipId).IsUnique();
            b.HasIndex(x => new { x.TenantId, x.CurrentTierId });
        });

        builder.Entity<PointsTransaction>(b =>
        {
            b.ToTable(EksabliConsts.DbTablePrefix + "PointsTransactions", EksabliConsts.DbSchema);
            b.ConfigureByConvention(); //auto configure for the base class props
            b.Property(x => x.WalletId).IsRequired();
            b.Property(x => x.Reason).HasMaxLength(PointsTransactionConsts.MaxReasonLength);
            b.Property(x => x.TierMultiplierSnapshot).HasColumnType("numeric(9,4)");
            b.HasIndex(x => new { x.WalletId, x.CreationTime });
            b.HasIndex(x => new { x.TenantId, x.ExpiresAt });
            b.HasIndex(x => new { x.TenantId, x.CreationTime });
        });

        builder.Entity<Tier>(b =>
        {
            b.ToTable(EksabliConsts.DbTablePrefix + "Tiers", EksabliConsts.DbSchema);
            b.ConfigureByConvention(); //auto configure for the base class props
            b.Property(x => x.Name).IsRequired().HasMaxLength(TierConsts.MaxNameLength);
            b.Property(x => x.Multiplier).HasColumnType("numeric(5,2)");
            b.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
        });

        builder.Entity<PointRule>(b =>
        {
            b.ToTable(EksabliConsts.DbTablePrefix + "PointRules", EksabliConsts.DbSchema);
            b.ConfigureByConvention(); //auto configure for the base class props
            b.Property(x => x.PointsPerUnit).HasColumnType("numeric(9,4)");
            b.HasIndex(x => new { x.TenantId, x.RuleType }).IsUnique();
        });

        builder.Entity<Reward>(b =>
        {
            b.ToTable(EksabliConsts.DbTablePrefix + "Rewards", EksabliConsts.DbSchema);
            b.ConfigureByConvention(); //auto configure for the base class props
            b.Property(x => x.NameAr).IsRequired().HasMaxLength(RewardConsts.MaxNameLength);
            b.Property(x => x.NameEn).IsRequired().HasMaxLength(RewardConsts.MaxNameLength);
            b.Property(x => x.ImageBlobName).HasMaxLength(RewardConsts.MaxImageBlobNameLength);
            b.HasIndex(x => new { x.TenantId, x.ValidFrom, x.ValidTo });
        });

        builder.Entity<Coupon>(b =>
        {
            b.ToTable(EksabliConsts.DbTablePrefix + "Coupons", EksabliConsts.DbSchema);
            b.ConfigureByConvention(); //auto configure for the base class props
            b.Property(x => x.Code).IsRequired().HasMaxLength(CouponConsts.CodeLength);
            b.HasIndex(x => x.Code).IsUnique();
            b.HasIndex(x => new { x.MembershipId, x.Status });
            b.HasIndex(x => new { x.TenantId, x.Status });
        });

        builder.Entity<SubscriptionPlan>(b =>
        {
            b.ToTable(EksabliConsts.DbTablePrefix + "SubscriptionPlans", EksabliConsts.DbSchema);
            b.ConfigureByConvention(); //auto configure for the base class props
            b.Property(x => x.Name).IsRequired().HasMaxLength(SubscriptionPlanConsts.MaxNameLength);
            b.Property(x => x.MonthlyPrice).HasColumnType("numeric(10,2)");
            b.Property(x => x.FeatureLimitsJson).HasMaxLength(SubscriptionPlanConsts.MaxFeatureLimitsJsonLength);
        });

        builder.Entity<TenantSubscription>(b =>
        {
            b.ToTable(EksabliConsts.DbTablePrefix + "TenantSubscriptions", EksabliConsts.DbSchema);
            b.ConfigureByConvention(); //auto configure for the base class props
            b.HasIndex(x => x.TenantId).IsUnique();
        });

        builder.Entity<Invoice>(b =>
        {
            b.ToTable(EksabliConsts.DbTablePrefix + "Invoices", EksabliConsts.DbSchema);
            b.ConfigureByConvention(); //auto configure for the base class props
            b.Property(x => x.Amount).HasColumnType("numeric(10,2)");
            b.HasIndex(x => new { x.TenantSubscriptionId, x.DueDate });
        });

        builder.Entity<Payment>(b =>
        {
            b.ToTable(EksabliConsts.DbTablePrefix + "Payments", EksabliConsts.DbSchema);
            b.ConfigureByConvention(); //auto configure for the base class props
            b.Property(x => x.Provider).IsRequired().HasMaxLength(PaymentConsts.MaxProviderLength);
            b.Property(x => x.ProviderTransactionRef).HasMaxLength(PaymentConsts.MaxProviderTransactionRefLength);
            b.HasIndex(x => x.InvoiceId);
        });

        builder.Entity<Campaign>(b =>
        {
            b.ToTable(EksabliConsts.DbTablePrefix + "Campaigns", EksabliConsts.DbSchema);
            b.ConfigureByConvention(); //auto configure for the base class props
            b.Property(x => x.NameAr).IsRequired().HasMaxLength(CampaignConsts.MaxNameLength);
            b.Property(x => x.NameEn).IsRequired().HasMaxLength(CampaignConsts.MaxNameLength);
            b.Property(x => x.RulesJson).HasMaxLength(CampaignConsts.MaxRulesJsonLength);
            b.HasIndex(x => new { x.TenantId, x.Status, x.StartDate });

            // Child collection, no separate repository — see CampaignTargetRule's own comment.
            b.HasMany(x => x.TargetRules).WithOne().HasForeignKey(x => x.CampaignId).OnDelete(DeleteBehavior.Cascade);
            b.Navigation(x => x.TargetRules).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        builder.Entity<CampaignTargetRule>(b =>
        {
            b.ToTable(EksabliConsts.DbTablePrefix + "CampaignTargetRules", EksabliConsts.DbSchema);
            b.ConfigureByConvention(); //auto configure for the base class props
            b.Property(x => x.ParametersJson).HasMaxLength(CampaignTargetRuleConsts.MaxParametersJsonLength);
            b.HasIndex(x => x.CampaignId);
        });

        builder.Entity<Offer>(b =>
        {
            b.ToTable(EksabliConsts.DbTablePrefix + "Offers", EksabliConsts.DbSchema);
            b.ConfigureByConvention(); //auto configure for the base class props
            b.Property(x => x.TitleAr).IsRequired().HasMaxLength(OfferConsts.MaxTitleLength);
            b.Property(x => x.TitleEn).IsRequired().HasMaxLength(OfferConsts.MaxTitleLength);
            b.Property(x => x.DescriptionAr).HasMaxLength(OfferConsts.MaxDescriptionLength);
            b.Property(x => x.DescriptionEn).HasMaxLength(OfferConsts.MaxDescriptionLength);
            b.Property(x => x.ImageBlobName).HasMaxLength(OfferConsts.MaxImageBlobNameLength);
            b.HasIndex(x => new { x.TenantId, x.StartDate, x.EndDate });
        });

        builder.Entity<Notification>(b =>
        {
            b.ToTable(EksabliConsts.DbTablePrefix + "Notifications", EksabliConsts.DbSchema);
            b.ConfigureByConvention(); //auto configure for the base class props
            b.Property(x => x.Title).IsRequired().HasMaxLength(NotificationConsts.MaxTitleLength);
            b.Property(x => x.Body).IsRequired().HasMaxLength(NotificationConsts.MaxBodyLength);
            b.HasIndex(x => new { x.TenantId, x.CampaignId });
            b.HasIndex(x => new { x.MembershipId, x.CreationTime });
        });

        builder.Entity<NotificationMessage>(b =>
        {
            b.ToTable(EksabliConsts.DbTablePrefix + "NotificationMessages", EksabliConsts.DbSchema);
            b.ConfigureByConvention(); //auto configure for the base class props
            b.Property(x => x.Title).IsRequired().HasMaxLength(UserNotificationConsts.MaxTitleLength);
            b.Property(x => x.Message).IsRequired().HasMaxLength(UserNotificationConsts.MaxMessageLength);
            b.Property(x => x.Category).HasMaxLength(UserNotificationConsts.MaxCategoryLength);
            b.Property(x => x.Data).HasMaxLength(UserNotificationConsts.MaxDataLength);
        });

        builder.Entity<UserNotification>(b =>
        {
            b.ToTable(EksabliConsts.DbTablePrefix + "UserNotifications", EksabliConsts.DbSchema);
            b.ConfigureByConvention(); //auto configure for the base class props
            // Feed query is "my unread/all, newest first" — see EfCoreUserNotificationRepository.GetFeedAsync.
            b.HasIndex(x => new { x.UserId, x.IsRead, x.CreationTime });
            b.HasIndex(x => x.NotificationMessageId);
        });

        builder.Entity<Referral>(b =>
        {
            b.ToTable(EksabliConsts.DbTablePrefix + "Referrals", EksabliConsts.DbSchema);
            b.ConfigureByConvention(); //auto configure for the base class props
            b.HasIndex(x => new { x.TenantId, x.Status });
            b.HasIndex(x => x.ReferrerMembershipId);
            b.HasIndex(x => x.RefereeCustomerId);
        });

        builder.Entity<Achievement>(b =>
        {
            b.ToTable(EksabliConsts.DbTablePrefix + "Achievements", EksabliConsts.DbSchema);
            b.ConfigureByConvention(); //auto configure for the base class props
            b.Property(x => x.Name).IsRequired().HasMaxLength(AchievementConsts.MaxNameLength);
            b.Property(x => x.CriteriaJson).HasMaxLength(AchievementConsts.MaxCriteriaJsonLength);
            b.HasIndex(x => x.TenantId);
        });

        builder.Entity<AchievementAward>(b =>
        {
            b.ToTable(EksabliConsts.DbTablePrefix + "AchievementAwards", EksabliConsts.DbSchema);
            b.ConfigureByConvention(); //auto configure for the base class props
            b.HasIndex(x => new { x.MembershipId, x.AchievementId }).IsUnique();
        });

        builder.Entity<Follow>(b =>
        {
            b.ToTable(EksabliConsts.DbTablePrefix + "Follows", EksabliConsts.DbSchema);
            b.ConfigureByConvention(); //auto configure for the base class props
            b.HasIndex(x => new { x.CustomerId, x.TenantId }).IsUnique();
        });

        builder.Entity<Category>(b =>
        {
            b.ToTable(EksabliConsts.DbTablePrefix + "Categories", EksabliConsts.DbSchema);
            b.ConfigureByConvention(); //auto configure for the base class props
            b.Property(x => x.NameAr).IsRequired().HasMaxLength(CategoryConsts.MaxNameLength);
            b.Property(x => x.NameEn).IsRequired().HasMaxLength(CategoryConsts.MaxNameLength);
            b.Property(x => x.IconBlobName).HasMaxLength(CategoryConsts.MaxIconBlobNameLength);
            b.HasIndex(x => x.ParentCategoryId);
            b.HasOne<Category>().WithMany().HasForeignKey(x => x.ParentCategoryId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<SupportTicket>(b =>
        {
            b.ToTable(EksabliConsts.DbTablePrefix + "SupportTickets", EksabliConsts.DbSchema);
            b.ConfigureByConvention(); //auto configure for the base class props
            b.Property(x => x.Subject).IsRequired().HasMaxLength(SupportTicketConsts.MaxSubjectLength);
            b.HasIndex(x => new { x.Status, x.Priority });
            b.HasIndex(x => x.TenantId);
            b.HasIndex(x => x.CustomerId);

            // Child collection, no separate repository — see SupportTicketMessage's own comment.
            b.HasMany(x => x.Messages).WithOne().HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Cascade);
            b.Navigation(x => x.Messages).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        builder.Entity<SupportTicketMessage>(b =>
        {
            b.ToTable(EksabliConsts.DbTablePrefix + "SupportTicketMessages", EksabliConsts.DbSchema);
            b.ConfigureByConvention(); //auto configure for the base class props
            b.Property(x => x.Body).IsRequired().HasMaxLength(SupportTicketMessageConsts.MaxBodyLength);
            b.HasIndex(x => new { x.TicketId, x.CreatedAt });
        });

        builder.Entity<SmsLog>(b =>
        {
            b.ToTable(EksabliConsts.DbTablePrefix + "SmsLogs", EksabliConsts.DbSchema);
            b.ConfigureByConvention(); //auto configure for the base class props
            b.Property(x => x.PhoneNumber).IsRequired().HasMaxLength(SmsLogConsts.MaxPhoneNumberLength);
            b.Property(x => x.Message).IsRequired().HasMaxLength(SmsLogConsts.MaxMessageLength);
            b.HasIndex(x => x.CreationTime);
            b.HasIndex(x => x.PhoneNumber);
        });
    }
}
