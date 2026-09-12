using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Eksabli.Campaigns;
using Eksabli.CustomerProfiles;
using Eksabli.EmployeeAssignments;
using Eksabli.Memberships;
using Eksabli.Rewards;
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
    private readonly IRewardRepository _rewardRepository;
    private readonly ICouponRepository _couponRepository;
    private readonly IDistributedCache _qrCache;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentPrincipalAccessor _currentPrincipalAccessor;
    private readonly IRepository<Campaign, Guid> _campaignRepository;

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
        _rewardRepository = GetRequiredService<IRewardRepository>();
        _couponRepository = GetRequiredService<ICouponRepository>();
        _qrCache = GetRequiredService<IDistributedCache>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _currentPrincipalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
        _campaignRepository = GetRequiredService<IRepository<Campaign, Guid>>();
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

    // Active as of "now" — CampaignRulesEngine.EvaluateAsync only ever considers Status=Active
    // campaigns whose StartDate/EndDate straddle the current moment (real-time evaluation mode, see
    // that class's own comment). StartDate is today (Campaign's own constructor rejects a start date
    // before today), which is already <= _clock.Now.
    private async Task<Guid> CreateActiveCampaignAsync(Guid tenantId, CampaignType type, string rulesJson)
    {
        Guid campaignId = default;
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                var campaign = Campaign.Create(
                    Guid.NewGuid(), "حملة اختبار", "Test Campaign", type, DateTime.UtcNow.Date, DateTime.UtcNow.Date.AddDays(30));
                campaign.SetRules(rulesJson);
                campaign.Activate();
                await _campaignRepository.InsertAsync(campaign, autoSave: true);
                campaignId = campaign.Id;
            }
        });
        return campaignId;
    }

    private async Task<Guid> CreateRewardAsync(Guid tenantId, int pointsCost, int? approvalThresholdPoints = null, DateTime? validTo = null)
    {
        Guid rewardId = default;
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                var reward = Reward.Create(Guid.NewGuid(), "مكافأة", "Reward", RewardType.Discount, pointsCost);
                reward.SetApprovalThresholdPoints(approvalThresholdPoints);
                reward.SetValidity(null, validTo);
                await _rewardRepository.InsertAsync(reward, autoSave: true);
                rewardId = reward.Id;
            }
        });
        return rewardId;
    }

    // Mirrors CouponAppService.RedeemAsync: a pending coupon and a matching hold on the wallet. The
    // reservation is not optional scaffolding — ConfirmRedemptionAsync commits it, and a coupon whose
    // points were never reserved would fail PointsWallet's own double-release guard.
    // `pointsCost` defaults to the reward's own cost rather than a literal: the fixture and the reward
    // must agree, or assertions about the debit are testing the fixture's number instead of the code's.
    private async Task<string> IssueCouponAsync(Guid tenantId, Guid membershipId, Guid rewardId, int? pointsCost = null)
    {
        var code = Guid.NewGuid().ToString("N")[..CouponConsts.CodeLength].ToUpperInvariant();
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                var cost = pointsCost ?? (await _rewardRepository.GetAsync(rewardId)).PointsCost;
                var coupon = Coupon.CreatePending(
                    Guid.NewGuid(),
                    rewardId,
                    membershipId,
                    code,
                    cost,
                    // Matches IClock.Now, now configured as DateTimeKind.Utc.
                    DateTime.UtcNow,
                    DateTime.UtcNow.AddMinutes(CouponConsts.PendingWindowMinutes));
                await _couponRepository.InsertAsync(coupon, autoSave: true);

                var wallet = await _walletRepository.SingleAsync(w => w.MembershipId == membershipId);
                wallet.ApplyTransaction(PointsTransactionType.Earn, cost);
                wallet.Reserve(cost);
                await _walletRepository.UpdateAsync(wallet, autoSave: true);
            }
        });
        return code;
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

    // Regression coverage for a real bug found live this session: AwardPointsCoreAsync used to write
    // ONE PointsTransaction (Source=Purchase, no ReferenceId) for the entire total, including a
    // real-time SpendXGetY campaign's flat bonus — leaving ReportsAppService.GetCampaignPerformanceAsync's
    // Source=Campaign/ReferenceId=<campaignId> query with nothing to find, so a campaign's "Rewarded
    // Members"/"Bonus Points Awarded" stats never moved no matter how many real sales applied it (unlike
    // CampaignSweepWorker's own batch-evaluated campaigns, which were always tagged correctly).
    [Fact]
    public async Task AwardPointsByCustomerIdAsync_Should_Attribute_The_SpendXGetY_Bonus_To_Its_Campaign()
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
            }
        });
        var campaignId = await CreateActiveCampaignAsync(tenantId, CampaignType.SpendXGetY, """{"spendThreshold":100,"bonusPoints":10}""");

        AwardPointsResultDto result = null!;
        using (_currentTenant.Change(tenantId))
        using (LoginAs(cashierId))
        {
            result = await WithUnitOfWorkAsync(() => _posAppService.AwardPointsByCustomerIdAsync(customerId, new AwardPointsByCustomerIdDto { PurchaseAmount = 200m }));
        }

        // 200 base (1 pt/$1, no tier) + 10 flat bonus = 210, split across two ledger rows.
        result.PointsAwarded.ShouldBe(210);
        result.NewBalance.ShouldBe(210);

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                var purchaseTx = await _transactionRepository.SingleAsync(t => t.WalletId == walletId && t.Source == PointsTransactionSource.Purchase);
                purchaseTx.Points.ShouldBe(200);

                var campaignTx = await _transactionRepository.SingleAsync(t => t.WalletId == walletId && t.Source == PointsTransactionSource.Campaign);
                campaignTx.Points.ShouldBe(10);
                campaignTx.ReferenceId.ShouldBe(campaignId);
            }
        });
    }

    // Guards against the split logic in AwardPointsCoreAsync firing even when there's nothing to
    // attribute — a pure DoublePoints multiplier campaign (no flat bonus at all) must still produce
    // exactly one Source=Purchase ledger row for the whole (multiplied) total, not an empty/zero second
    // row, since CampaignRulesEvaluationResult.BonusCampaignId is null in this case.
    [Fact]
    public async Task AwardPointsByCustomerIdAsync_Should_Not_Split_The_Ledger_When_Only_A_Multiplier_Campaign_Applies()
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
            }
        });
        await CreateActiveCampaignAsync(tenantId, CampaignType.DoublePoints, """{"multiplier":2}""");

        AwardPointsResultDto result = null!;
        using (_currentTenant.Change(tenantId))
        using (LoginAs(cashierId))
        {
            result = await WithUnitOfWorkAsync(() => _posAppService.AwardPointsByCustomerIdAsync(customerId, new AwardPointsByCustomerIdDto { PurchaseAmount = 50m }));
        }

        result.PointsAwarded.ShouldBe(100); // 50 * 2, no separate bonus

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                var transactions = await _transactionRepository.GetListAsync(t => t.WalletId == walletId);
                transactions.ShouldHaveSingleItem();
                transactions.Single().Source.ShouldBe(PointsTransactionSource.Purchase);
                transactions.Single().Points.ShouldBe(100);
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

    [Fact]
    public async Task Should_Confirm_Redemption_And_Mark_Coupon_Redeemed()
    {
        var tenantId = await CreateTenantAsync();
        var cashierId = await CreateStaffAsync(tenantId, EmployeeRole.Cashier);
        var (customerId, _) = await CreateCustomerAsync();
        var walletId = await JoinBusinessAsync(tenantId, customerId);
        var membershipId = await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                var wallet = await _walletRepository.GetAsync(walletId);
                return wallet.MembershipId;
            }
        });
        var rewardId = await CreateRewardAsync(tenantId, pointsCost: 50);
        var code = await IssueCouponAsync(tenantId, membershipId, rewardId);

        using (_currentTenant.Change(tenantId))
        using (LoginAs(cashierId))
        {
            var result = await WithUnitOfWorkAsync(() => _posAppService.ConfirmRedemptionAsync(new ConfirmRedemptionDto { Code = code }));
            result.RewardNameEn.ShouldBe("Reward");
        }

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                var coupon = await _couponRepository.SingleAsync(c => c.Code == code);
                coupon.Status.ShouldBe(CouponStatus.Redeemed);
                coupon.RedeemedByEmployeeId.ShouldBe(cashierId);

                // Approval is where the points actually move: the hold is gone and the balance has
                // dropped by the coupon's cost. Before this change the debit happened at redeem time,
                // which is the bug the whole reservation flow exists to fix.
                var wallet = await _walletRepository.GetAsync(walletId);
                wallet.Reserved.ShouldBe(0);
                wallet.Balance.ShouldBe(0);
                wallet.LifetimeRedeemed.ShouldBe(50);

                // ...and it is recorded in the ledger, by the employee who approved it.
                var ledger = await _transactionRepository.GetListAsync(x => x.WalletId == walletId);
                var redeem = ledger.Single(x => x.Type == PointsTransactionType.Redeem);
                redeem.Points.ShouldBe(-50);
                redeem.ReferenceId.ShouldBe(coupon.Id);
                redeem.CreatedByEmployeeId.ShouldBe(cashierId);
            }
        });
    }

    [Fact]
    public async Task Should_Return_The_Hold_When_Staff_Decline_A_Redemption()
    {
        var tenantId = await CreateTenantAsync();
        var cashierId = await CreateStaffAsync(tenantId, EmployeeRole.Cashier);
        var (customerId, _) = await CreateCustomerAsync();
        var walletId = await JoinBusinessAsync(tenantId, customerId);
        var membershipId = await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                var wallet = await _walletRepository.GetAsync(walletId);
                return wallet.MembershipId;
            }
        });
        var rewardId = await CreateRewardAsync(tenantId, pointsCost: 50);
        var code = await IssueCouponAsync(tenantId, membershipId, rewardId, pointsCost: 50);

        using (_currentTenant.Change(tenantId))
        using (LoginAs(cashierId))
        {
            var result = await WithUnitOfWorkAsync(() => _posAppService.RejectRedemptionAsync(
                new RejectRedemptionDto { Code = code, Reason = "Out of stock at this branch" }));

            result.PointsReleased.ShouldBe(50);
            result.NewAvailableBalance.ShouldBe(50);
        }

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                var coupon = await _couponRepository.SingleAsync(c => c.Code == code);
                coupon.Status.ShouldBe(CouponStatus.Cancelled);
                coupon.RejectionReason.ShouldBe("Out of stock at this branch");

                var wallet = await _walletRepository.GetAsync(walletId);
                wallet.Reserved.ShouldBe(0);
                wallet.Balance.ShouldBe(50);

                // A declined redemption never moved points, so it must leave no trace in the ledger —
                // the customer's history is not a log of things that did not happen.
                var ledger = await _transactionRepository.GetListAsync(x => x.WalletId == walletId);
                ledger.ShouldNotContain(x => x.Type == PointsTransactionType.Redeem);
            }
        });
    }

    [Fact]
    public async Task Should_Let_A_Cashier_Decline_A_Reward_They_Could_Not_Approve()
    {
        var tenantId = await CreateTenantAsync();
        var cashierId = await CreateStaffAsync(tenantId, EmployeeRole.Cashier);
        var (customerId, _) = await CreateCustomerAsync();
        var walletId = await JoinBusinessAsync(tenantId, customerId);
        var membershipId = await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                var wallet = await _walletRepository.GetAsync(walletId);
                return wallet.MembershipId;
            }
        });
        var rewardId = await CreateRewardAsync(tenantId, pointsCost: 500, approvalThresholdPoints: 300);
        var code = await IssueCouponAsync(tenantId, membershipId, rewardId, pointsCost: 500);

        // Refusing to hand something over is never the privileged direction: a cashier who cannot
        // approve a manager-only reward must still be able to give the customer their points back,
        // rather than leaving them held while someone hunts for a manager.
        using (_currentTenant.Change(tenantId))
        using (LoginAs(cashierId))
        {
            var result = await WithUnitOfWorkAsync(() => _posAppService.RejectRedemptionAsync(new RejectRedemptionDto { Code = code }));
            result.PointsReleased.ShouldBe(500);
        }
    }

    [Fact]
    public async Task Should_Not_Move_Points_When_Staff_Only_Look_A_Code_Up()
    {
        var tenantId = await CreateTenantAsync();
        var cashierId = await CreateStaffAsync(tenantId, EmployeeRole.Cashier);
        var (customerId, _) = await CreateCustomerAsync();
        var walletId = await JoinBusinessAsync(tenantId, customerId);
        var membershipId = await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                var wallet = await _walletRepository.GetAsync(walletId);
                return wallet.MembershipId;
            }
        });
        var rewardId = await CreateRewardAsync(tenantId, pointsCost: 50);
        var code = await IssueCouponAsync(tenantId, membershipId, rewardId, pointsCost: 50);

        using (_currentTenant.Change(tenantId))
        using (LoginAs(cashierId))
        {
            // Spaced and lower-cased, the way a cashier reading it off a phone would type it.
            var lookup = await WithUnitOfWorkAsync(() => _posAppService.LookupRedemptionAsync(
                new LookupRedemptionDto { Code = $"{code[..4].ToLowerInvariant()} {code[4..].ToLowerInvariant()}" }));

            lookup.Code.ShouldBe(code);
            lookup.PointsCost.ShouldBe(50);
            lookup.CanCurrentEmployeeApprove.ShouldBeTrue();
            lookup.RequiresManagerApproval.ShouldBeFalse();
        }

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                // A mis-scan has to be free: looking at a code decides nothing.
                var coupon = await _couponRepository.SingleAsync(c => c.Code == code);
                coupon.Status.ShouldBe(CouponStatus.Pending);

                var wallet = await _walletRepository.GetAsync(walletId);
                wallet.Reserved.ShouldBe(50);
                wallet.Balance.ShouldBe(50);
            }
        });
    }

    [Fact]
    public async Task Should_Tell_A_Cashier_They_Cannot_Approve_A_HighValue_Reward()
    {
        var tenantId = await CreateTenantAsync();
        var cashierId = await CreateStaffAsync(tenantId, EmployeeRole.Cashier);
        var (customerId, _) = await CreateCustomerAsync();
        var walletId = await JoinBusinessAsync(tenantId, customerId);
        var membershipId = await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                var wallet = await _walletRepository.GetAsync(walletId);
                return wallet.MembershipId;
            }
        });
        var rewardId = await CreateRewardAsync(tenantId, pointsCost: 500, approvalThresholdPoints: 300);
        var code = await IssueCouponAsync(tenantId, membershipId, rewardId, pointsCost: 500);

        // Advisory on lookup, enforced on confirm — so the UI can say "fetch a manager" up front
        // instead of letting them press Approve and eat an opaque 403.
        using (_currentTenant.Change(tenantId))
        using (LoginAs(cashierId))
        {
            var lookup = await WithUnitOfWorkAsync(() => _posAppService.LookupRedemptionAsync(new LookupRedemptionDto { Code = code }));
            lookup.RequiresManagerApproval.ShouldBeTrue();
            lookup.CanCurrentEmployeeApprove.ShouldBeFalse();
        }
    }

    [Fact]
    public async Task Should_Reject_Confirm_Redemption_For_Wrong_Or_AlreadyUsed_Code()
    {
        var tenantId = await CreateTenantAsync();
        var cashierId = await CreateStaffAsync(tenantId, EmployeeRole.Cashier);
        var (customerId, _) = await CreateCustomerAsync();
        var walletId = await JoinBusinessAsync(tenantId, customerId);
        var membershipId = await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                var wallet = await _walletRepository.GetAsync(walletId);
                return wallet.MembershipId;
            }
        });
        var rewardId = await CreateRewardAsync(tenantId, pointsCost: 50);
        var code = await IssueCouponAsync(tenantId, membershipId, rewardId);

        using (_currentTenant.Change(tenantId))
        using (LoginAs(cashierId))
        {
            await Assert.ThrowsAsync<UserFriendlyException>(async () =>
            {
                await WithUnitOfWorkAsync(() => _posAppService.ConfirmRedemptionAsync(new ConfirmRedemptionDto { Code = "BADCODE1" }));
            });

            await WithUnitOfWorkAsync(() => _posAppService.ConfirmRedemptionAsync(new ConfirmRedemptionDto { Code = code }));

            await Assert.ThrowsAsync<UserFriendlyException>(async () =>
            {
                await WithUnitOfWorkAsync(() => _posAppService.ConfirmRedemptionAsync(new ConfirmRedemptionDto { Code = code }));
            });
        }
    }

    [Fact]
    public async Task Should_Reject_And_Mark_Expired_When_Reward_Validity_Has_Passed()
    {
        var tenantId = await CreateTenantAsync();
        var cashierId = await CreateStaffAsync(tenantId, EmployeeRole.Cashier);
        var (customerId, _) = await CreateCustomerAsync();
        var walletId = await JoinBusinessAsync(tenantId, customerId);
        var membershipId = await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                var wallet = await _walletRepository.GetAsync(walletId);
                return wallet.MembershipId;
            }
        });
        var rewardId = await CreateRewardAsync(tenantId, pointsCost: 50, validTo: DateTime.UtcNow.AddDays(-1));
        var code = await IssueCouponAsync(tenantId, membershipId, rewardId);

        using (_currentTenant.Change(tenantId))
        using (LoginAs(cashierId))
        {
            await Assert.ThrowsAsync<UserFriendlyException>(async () =>
            {
                await WithUnitOfWorkAsync(() => _posAppService.ConfirmRedemptionAsync(new ConfirmRedemptionDto { Code = code }));
            });
        }

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                var coupon = await _couponRepository.SingleAsync(c => c.Code == code);
                coupon.Status.ShouldBe(CouponStatus.Expired);
            }
        });
    }

    [Fact]
    public async Task Should_Escalate_HighValue_Reward_Confirmation_To_Manager()
    {
        var tenantId = await CreateTenantAsync();
        var cashierId = await CreateStaffAsync(tenantId, EmployeeRole.Cashier);
        var managerId = await CreateStaffAsync(tenantId, EmployeeRole.BranchManager);
        var (customerId, _) = await CreateCustomerAsync();
        var walletId = await JoinBusinessAsync(tenantId, customerId);
        var membershipId = await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                var wallet = await _walletRepository.GetAsync(walletId);
                return wallet.MembershipId;
            }
        });
        var rewardId = await CreateRewardAsync(tenantId, pointsCost: 500, approvalThresholdPoints: 300);
        var code = await IssueCouponAsync(tenantId, membershipId, rewardId);

        using (_currentTenant.Change(tenantId))
        using (LoginAs(cashierId))
        {
            await Assert.ThrowsAsync<AbpAuthorizationException>(async () =>
            {
                await WithUnitOfWorkAsync(() => _posAppService.ConfirmRedemptionAsync(new ConfirmRedemptionDto { Code = code }));
            });
        }

        using (_currentTenant.Change(tenantId))
        using (LoginAs(managerId))
        {
            var result = await WithUnitOfWorkAsync(() => _posAppService.ConfirmRedemptionAsync(new ConfirmRedemptionDto { Code = code }));
            result.CouponId.ShouldNotBe(Guid.Empty);
        }
    }
}
