using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Eksabli.CustomerProfiles;
using Eksabli.EmployeeAssignments;
using Eksabli.Memberships;
using Eksabli.Wallets;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Distributed;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Volo.Abp.TenantManagement;
using Xunit;

namespace Eksabli.Pos;

public abstract class PosAppService_Tests<TStartupModule> : EksabliApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IPosAppService _posAppService;
    private readonly TenantManager _tenantManager;
    private readonly ITenantRepository _tenantRepository;
    private readonly IdentityUserManager _identityUserManager;
    private readonly IRepository<Membership, Guid> _membershipRepository;
    private readonly IRepository<PointsWallet, Guid> _walletRepository;
    private readonly IRepository<PointsTransaction, Guid> _transactionRepository;
    private readonly IRepository<Wallets.Tier, Guid> _tierRepository;
    private readonly IRepository<PointRule, Guid> _pointRuleRepository;
    private readonly IRepository<EmployeeAssignment, Guid> _employeeAssignmentRepository;
    private readonly IRepository<CustomerProfile, Guid> _customerProfileRepository;
    private readonly IDistributedCache _qrCache;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentPrincipalAccessor _currentPrincipalAccessor;

    protected PosAppService_Tests()
    {
        _posAppService = GetRequiredService<IPosAppService>();
        _tenantManager = GetRequiredService<TenantManager>();
        _tenantRepository = GetRequiredService<ITenantRepository>();
        _identityUserManager = GetRequiredService<IdentityUserManager>();
        _membershipRepository = GetRequiredService<IRepository<Membership, Guid>>();
        _walletRepository = GetRequiredService<IRepository<PointsWallet, Guid>>();
        _transactionRepository = GetRequiredService<IRepository<PointsTransaction, Guid>>();
        _tierRepository = GetRequiredService<IRepository<Wallets.Tier, Guid>>();
        _pointRuleRepository = GetRequiredService<IRepository<PointRule, Guid>>();
        _employeeAssignmentRepository = GetRequiredService<IRepository<EmployeeAssignment, Guid>>();
        _customerProfileRepository = GetRequiredService<IRepository<CustomerProfile, Guid>>();
        _qrCache = GetRequiredService<IDistributedCache>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _currentPrincipalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    private IDisposable LoginAs(Guid userId)
    {
        var identity = new ClaimsIdentity("Test");
        identity.AddClaim(new Claim(AbpClaimTypes.UserId, userId.ToString()));
        return _currentPrincipalAccessor.Change(new ClaimsPrincipal(identity));
    }

    private async Task<Guid> CreateTenantAsync()
    {
        Guid tenantId = default;
        await WithUnitOfWorkAsync(async () =>
        {
            var tenant = await _tenantManager.CreateAsync("tenant-" + Guid.NewGuid().ToString("N"));
            await _tenantRepository.InsertAsync(tenant, autoSave: true);
            tenantId = tenant.Id;
        });
        return tenantId;
    }

    private async Task<Guid> CreateStaffAsync(Guid tenantId, EmployeeRole role)
    {
        Guid userId = default;
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                var email = $"{role}-{Guid.NewGuid():N}@example.com";
                var user = new IdentityUser(Guid.NewGuid(), email, email, tenantId);
                (await _identityUserManager.CreateAsync(user)).CheckErrors();
                userId = user.Id;

                var assignment = EmployeeAssignment.Create(Guid.NewGuid(), userId, role);
                await _employeeAssignmentRepository.InsertAsync(assignment, autoSave: true);
            }
        });
        return userId;
    }

    private async Task<(Guid customerId, string phoneNumber)> CreateCustomerAsync()
    {
        Guid customerId = default;
        var phoneNumber = "+1555" + Random.Shared.Next(1000000, 9999999);

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(null))
            {
                var user = new IdentityUser(Guid.NewGuid(), phoneNumber, $"{Guid.NewGuid():N}@otp.eksabli.local", tenantId: null);
                (await _identityUserManager.CreateAsync(user)).CheckErrors();
                await _identityUserManager.SetPhoneNumberAsync(user, phoneNumber);
                customerId = user.Id;

                var profile = CustomerProfile.Create(Guid.NewGuid(), customerId);
                profile.SetName("Jane", "Doe");
                await _customerProfileRepository.InsertAsync(profile, autoSave: true);
            }
        });

        return (customerId, phoneNumber);
    }

    private async Task<Guid> JoinBusinessAsync(Guid tenantId, Guid customerId)
    {
        Guid walletId = default;
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                var membership = Membership.Create(Guid.NewGuid(), customerId, DateTime.UtcNow);
                await _membershipRepository.InsertAsync(membership, autoSave: true);

                var wallet = PointsWallet.Create(Guid.NewGuid(), membership.Id);
                await _walletRepository.InsertAsync(wallet, autoSave: true);
                walletId = wallet.Id;
            }
        });
        return walletId;
    }

    [Fact]
    public async Task Should_Apply_Tier_Multiplier_And_Floor_The_Result()
    {
        var tenantId = await CreateTenantAsync();
        var cashierId = await CreateStaffAsync(tenantId, EmployeeRole.Cashier);
        var (customerId, _) = await CreateCustomerAsync();
        var walletId = await JoinBusinessAsync(tenantId, customerId);

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                await _pointRuleRepository.InsertAsync(PointRule.Create(Guid.NewGuid(), PointRuleType.PerCurrencyUnit, 1m), autoSave: true);

                var gold = Wallets.Tier.Create(Guid.NewGuid(), "Gold", 0, 1.5m);
                await _tierRepository.InsertAsync(gold, autoSave: true);

                var wallet = await _walletRepository.GetAsync(walletId);
                wallet.ChangeTier(gold.Id);
                await _walletRepository.UpdateAsync(wallet);
            }
        });

        AwardPointsResultDto result = null!;
        using (_currentTenant.Change(tenantId))
        using (LoginAs(cashierId))
        {
            result = await WithUnitOfWorkAsync(() => _posAppService.AwardPointsByCustomerIdAsync(customerId, new AwardPointsByCustomerIdDto { PurchaseAmount = 2.5m }));
        }

        // 2.5 base * 1.5 multiplier = 3.75 -> floor -> 3
        result.PointsAwarded.ShouldBe(3);
        result.NewBalance.ShouldBe(3);

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                var tx = await _transactionRepository.GetAsync(result.TransactionId);
                tx.TierMultiplierSnapshot.ShouldBe(1.5m);
            }
        });
    }

    [Fact]
    public async Task Should_Fall_Back_To_PerVisit_Rule_When_No_Purchase_Amount_Given()
    {
        var tenantId = await CreateTenantAsync();
        var cashierId = await CreateStaffAsync(tenantId, EmployeeRole.Cashier);
        var (customerId, _) = await CreateCustomerAsync();
        await JoinBusinessAsync(tenantId, customerId);

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                await _pointRuleRepository.InsertAsync(PointRule.Create(Guid.NewGuid(), PointRuleType.PerVisit, 10m), autoSave: true);
            }
        });

        AwardPointsResultDto result = null!;
        using (_currentTenant.Change(tenantId))
        using (LoginAs(cashierId))
        {
            result = await WithUnitOfWorkAsync(() => _posAppService.AwardPointsByCustomerIdAsync(customerId, new AwardPointsByCustomerIdDto()));
        }

        result.PointsAwarded.ShouldBe(10);
    }

    [Fact]
    public async Task Should_Auto_Upgrade_Tier_And_Snapshot_The_PreUpgrade_Multiplier()
    {
        var tenantId = await CreateTenantAsync();
        var cashierId = await CreateStaffAsync(tenantId, EmployeeRole.Cashier);
        var (customerId, _) = await CreateCustomerAsync();
        var walletId = await JoinBusinessAsync(tenantId, customerId);

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                await _pointRuleRepository.InsertAsync(PointRule.Create(Guid.NewGuid(), PointRuleType.PerCurrencyUnit, 10m), autoSave: true);

                var silver = Wallets.Tier.Create(Guid.NewGuid(), "Silver", 0, 1.0m);
                var gold = Wallets.Tier.Create(Guid.NewGuid(), "Gold", 50, 2.0m);
                await _tierRepository.InsertAsync(silver, autoSave: true);
                await _tierRepository.InsertAsync(gold, autoSave: true);

                var wallet = await _walletRepository.GetAsync(walletId);
                wallet.ChangeTier(silver.Id);
                await _walletRepository.UpdateAsync(wallet);
            }
        });

        AwardPointsResultDto result = null!;
        using (_currentTenant.Change(tenantId))
        using (LoginAs(cashierId))
        {
            result = await WithUnitOfWorkAsync(() => _posAppService.AwardPointsByCustomerIdAsync(customerId, new AwardPointsByCustomerIdDto { PurchaseAmount = 6m }));
        }

        result.PointsAwarded.ShouldBe(60); // 6 * 10 * 1.0 (Silver, pre-upgrade)
        result.NewTierName.ShouldBe("Gold"); // LifetimeEarned=60 >= Gold's 50 threshold, upgraded after award

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                var tx = await _transactionRepository.GetAsync(result.TransactionId);
                tx.TierMultiplierSnapshot.ShouldBe(1.0m); // snapshot uses the PRE-upgrade (Silver) multiplier
            }
        });
    }

    [Fact]
    public async Task Should_Burn_QR_Token_On_Successful_Award_And_Reject_Reuse()
    {
        var tenantId = await CreateTenantAsync();
        var cashierId = await CreateStaffAsync(tenantId, EmployeeRole.Cashier);
        var (customerId, _) = await CreateCustomerAsync();
        await JoinBusinessAsync(tenantId, customerId);

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                await _pointRuleRepository.InsertAsync(PointRule.Create(Guid.NewGuid(), PointRuleType.PerVisit, 5m), autoSave: true);
            }
        });

        var token = Guid.NewGuid().ToString("N");
        var item = new WalletQrCacheItem { CustomerId = customerId };
        await WithUnitOfWorkAsync(() => _qrCache.SetAsync(WalletQrCacheItem.CacheKeyPrefix + token, JsonSerializer.SerializeToUtf8Bytes(item),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(90) }));

        using (_currentTenant.Change(tenantId))
        using (LoginAs(cashierId))
        {
            var firstAward = await WithUnitOfWorkAsync(() => _posAppService.AwardPointsByQrAsync(new AwardPointsByQrDto { QrToken = token }));
            firstAward.PointsAwarded.ShouldBe(5);

            await Assert.ThrowsAsync<AbpAuthorizationException>(async () =>
            {
                await WithUnitOfWorkAsync(() => _posAppService.AwardPointsByQrAsync(new AwardPointsByQrDto { QrToken = token }));
            });
        }
    }

    [Fact]
    public async Task Should_Lookup_Customer_By_Exact_Phone_Match_Only()
    {
        var tenantId = await CreateTenantAsync();
        var cashierId = await CreateStaffAsync(tenantId, EmployeeRole.Cashier);
        var (customerId, phoneNumber) = await CreateCustomerAsync();
        await JoinBusinessAsync(tenantId, customerId);

        using (_currentTenant.Change(tenantId))
        using (LoginAs(cashierId))
        {
            var result = await WithUnitOfWorkAsync(() => _posAppService.LookupCustomerByPhoneAsync(new PhoneLookupDto { PhoneNumber = phoneNumber }));
            result.CustomerId.ShouldBe(customerId);
            result.FirstName.ShouldBe("Jane");
        }
    }

    [Fact]
    public async Task Should_Give_Identical_Error_For_Unknown_Phone_And_NonMember_Phone()
    {
        var tenantId = await CreateTenantAsync();
        var cashierId = await CreateStaffAsync(tenantId, EmployeeRole.Cashier);

        // A real customer, but NOT a member of this tenant.
        var (_, nonMemberPhone) = await CreateCustomerAsync();

        string? unknownPhoneMessage = null;
        string? nonMemberMessage = null;

        using (_currentTenant.Change(tenantId))
        using (LoginAs(cashierId))
        {
            var unknownEx = await Assert.ThrowsAsync<UserFriendlyException>(async () =>
            {
                await WithUnitOfWorkAsync(() => _posAppService.LookupCustomerByPhoneAsync(
                    new PhoneLookupDto { PhoneNumber = "+15559999999" }));
            });
            unknownPhoneMessage = unknownEx.Message;

            var nonMemberEx = await Assert.ThrowsAsync<UserFriendlyException>(async () =>
            {
                await WithUnitOfWorkAsync(() => _posAppService.LookupCustomerByPhoneAsync(
                    new PhoneLookupDto { PhoneNumber = nonMemberPhone }));
            });
            nonMemberMessage = nonMemberEx.Message;
        }

        nonMemberMessage.ShouldBe(unknownPhoneMessage);
    }

    [Fact]
    public async Task Should_Isolate_Phone_Lookup_By_Tenant()
    {
        var tenantA = await CreateTenantAsync();
        var tenantB = await CreateTenantAsync();
        var cashierB = await CreateStaffAsync(tenantB, EmployeeRole.Cashier);

        var (customerId, phoneNumber) = await CreateCustomerAsync();
        await JoinBusinessAsync(tenantA, customerId); // member of A only

        using (_currentTenant.Change(tenantB))
        using (LoginAs(cashierB))
        {
            await Assert.ThrowsAsync<UserFriendlyException>(async () =>
            {
                await WithUnitOfWorkAsync(() => _posAppService.LookupCustomerByPhoneAsync(new PhoneLookupDto { PhoneNumber = phoneNumber }));
            });
        }
    }

    [Fact]
    public async Task Should_Reject_Award_For_A_NonMember_Without_Auto_Joining()
    {
        var tenantId = await CreateTenantAsync();
        var cashierId = await CreateStaffAsync(tenantId, EmployeeRole.Cashier);
        var (customerId, _) = await CreateCustomerAsync(); // never joins

        using (_currentTenant.Change(tenantId))
        using (LoginAs(cashierId))
        {
            await Assert.ThrowsAsync<UserFriendlyException>(async () =>
            {
                await WithUnitOfWorkAsync(() => _posAppService.AwardPointsByCustomerIdAsync(customerId, new AwardPointsByCustomerIdDto { PurchaseAmount = 10m }));
            });
        }

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                var membership = await _membershipRepository.FirstOrDefaultAsync(m => m.CustomerId == customerId);
                membership.ShouldBeNull();
            }
        });
    }

    [Fact]
    public async Task Should_Reject_Award_From_A_Role_Without_Award_Permission()
    {
        var tenantId = await CreateTenantAsync();
        var marketingManagerId = await CreateStaffAsync(tenantId, EmployeeRole.MarketingManager);
        var (customerId, _) = await CreateCustomerAsync();
        await JoinBusinessAsync(tenantId, customerId);

        using (_currentTenant.Change(tenantId))
        using (LoginAs(marketingManagerId))
        {
            await Assert.ThrowsAsync<AbpAuthorizationException>(async () =>
            {
                await WithUnitOfWorkAsync(() => _posAppService.AwardPointsByCustomerIdAsync(customerId, new AwardPointsByCustomerIdDto { PurchaseAmount = 10m }));
            });
        }
    }

    [Fact]
    public async Task Should_Record_Manual_Adjustment_With_Employee_And_Reason()
    {
        var tenantId = await CreateTenantAsync();
        var managerId = await CreateStaffAsync(tenantId, EmployeeRole.BranchManager);
        var (customerId, _) = await CreateCustomerAsync();
        await JoinBusinessAsync(tenantId, customerId);

        AwardPointsResultDto result = null!;
        using (_currentTenant.Change(tenantId))
        using (LoginAs(managerId))
        {
            result = await WithUnitOfWorkAsync(() => _posAppService.ManualAdjustAsync(new ManualAdjustDto
            {
                CustomerId = customerId,
                Points = 50,
                Reason = "Goodwill gesture"
            }));
        }

        result.PointsAwarded.ShouldBe(50);
        result.NewBalance.ShouldBe(50);

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                var tx = await _transactionRepository.GetAsync(result.TransactionId);
                tx.CreatedByEmployeeId.ShouldBe(managerId);
                tx.Reason.ShouldBe("Goodwill gesture");
                tx.Type.ShouldBe(PointsTransactionType.Adjust);
            }
        });
    }

    [Fact]
    public async Task Should_Reject_Manual_Adjustment_From_Cashier_Role()
    {
        var tenantId = await CreateTenantAsync();
        var cashierId = await CreateStaffAsync(tenantId, EmployeeRole.Cashier);
        var (customerId, _) = await CreateCustomerAsync();
        await JoinBusinessAsync(tenantId, customerId);

        using (_currentTenant.Change(tenantId))
        using (LoginAs(cashierId))
        {
            await Assert.ThrowsAsync<AbpAuthorizationException>(async () =>
            {
                await WithUnitOfWorkAsync(() => _posAppService.ManualAdjustAsync(new ManualAdjustDto { CustomerId = customerId, Points = 10 }));
            });
        }
    }

    [Fact]
    public async Task Should_Enforce_Daily_Manual_Adjustment_Cap()
    {
        var tenantId = await CreateTenantAsync();
        var managerId = await CreateStaffAsync(tenantId, EmployeeRole.Owner);
        var (customerId, _) = await CreateCustomerAsync();
        await JoinBusinessAsync(tenantId, customerId);

        using (_currentTenant.Change(tenantId))
        using (LoginAs(managerId))
        {
            for (var i = 0; i < PointsTransactionConsts.MaxDailyManualAdjustmentsPerEmployee; i++)
            {
                await WithUnitOfWorkAsync(() => _posAppService.ManualAdjustAsync(new ManualAdjustDto { CustomerId = customerId, Points = 1 }));
            }

            await Assert.ThrowsAsync<UserFriendlyException>(async () =>
            {
                await WithUnitOfWorkAsync(() => _posAppService.ManualAdjustAsync(new ManualAdjustDto { CustomerId = customerId, Points = 1 }));
            });
        }
    }
}
