using Microsoft.EntityFrameworkCore;
using Volo.Abp.AuditLogging.EntityFrameworkCore;
using Eksabli.Authors;
using Eksabli.Books;
using Eksabli.Memberships;
using Eksabli.BusinessProfiles;
using Eksabli.Branches;
using Eksabli.CustomerProfiles;
using Eksabli.EmployeeAssignments;
using Eksabli.Devices;
using Eksabli.Wallets;
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

    public DbSet<Author> Authors { get; set; }

    public DbSet<Book> Books { get; set; }

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

        builder.Entity<Author>(b =>
        {
            b.ToTable(EksabliConsts.DbTablePrefix + "Authors",
                EksabliConsts.DbSchema);
            b.ConfigureByConvention(); //auto configure for the base class props
            b.Property(x => x.Name).IsRequired().HasMaxLength(AuthorConsts.MaxNameLength);
            b.Property(x => x.ShortBio).HasMaxLength(AuthorConsts.MaxShortBioLength);
        });

        builder.Entity<Book>(b =>
        {
            b.ToTable(EksabliConsts.DbTablePrefix + "Books",
                EksabliConsts.DbSchema);
            b.ConfigureByConvention(); //auto configure for the base class props
            b.Property(x => x.Name).IsRequired().HasMaxLength(128);
            b.HasOne<Author>().WithMany().HasForeignKey(x => x.AuthorId).IsRequired();
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
    }
}
