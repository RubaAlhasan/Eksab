using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Eksabli.BusinessProfiles;
using Eksabli.EmployeeAssignments;
using Eksabli.Memberships;
using Eksabli.Wallets;
using Eksabli.Platform;
using Eksabli.Reports;
using Eksabli.Shared;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Authorization;
using Volo.Abp.Content;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Volo.Abp.TenantManagement;
using Xunit;

namespace Eksabli.SmartOffers;

// Application-level behaviour against the real persistence layer (SQLite in tests). The deal used throughout is an
// always-on stage (00:00–24:00 in UTC), so these tests are not sensitive to the time of day they run at. The
// time-window rules themselves are covered by the pure domain tests.
public abstract class SmartOfferAppService_Tests<TStartupModule> : EksabliApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly ISmartOfferAppService _offerService;
    private readonly ICustomerSmartOfferAppService _customerService;
    private readonly ISmartOfferOrderAppService _staffService;
    private readonly IReportsAppService _reportsService;
    private readonly IAdminUserAppService _adminUserService;
    private readonly TenantManager _tenantManager;
    private readonly ITenantRepository _tenantRepository;
    private readonly IRepository<Membership, Guid> _membershipRepository;
    private readonly IRepository<EmployeeAssignment, Guid> _employeeAssignmentRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentPrincipalAccessor _currentPrincipalAccessor;
    private readonly IRepository<PointsWallet, Guid> _walletRepository;
    private readonly IRepository<PointRule, Guid> _pointRuleRepository;
    private readonly IRepository<PointsTransaction, Guid> _transactionRepository;

    protected SmartOfferAppService_Tests()
    {
        _offerService = GetRequiredService<ISmartOfferAppService>();
        _customerService = GetRequiredService<ICustomerSmartOfferAppService>();
        _staffService = GetRequiredService<ISmartOfferOrderAppService>();
        _reportsService = GetRequiredService<IReportsAppService>();
        _adminUserService = GetRequiredService<IAdminUserAppService>();
        _tenantManager = GetRequiredService<TenantManager>();
        _tenantRepository = GetRequiredService<ITenantRepository>();
        _membershipRepository = GetRequiredService<IRepository<Membership, Guid>>();
        _employeeAssignmentRepository = GetRequiredService<IRepository<EmployeeAssignment, Guid>>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _currentPrincipalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
        _walletRepository = GetRequiredService<IRepository<PointsWallet, Guid>>();
        _pointRuleRepository = GetRequiredService<IRepository<PointRule, Guid>>();
        _transactionRepository = GetRequiredService<IRepository<PointsTransaction, Guid>>();
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

    private async Task JoinAsync(Guid tenantId, Guid customerId)
    {
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                await _membershipRepository.InsertAsync(Membership.Create(Guid.NewGuid(), customerId, DateTime.UtcNow), autoSave: true);
            }
        });
    }

    private async Task<Guid> CreateStaffAsync(Guid tenantId, EmployeeRole role)
    {
        var userId = Guid.NewGuid();
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                await _employeeAssignmentRepository.InsertAsync(EmployeeAssignment.Create(Guid.NewGuid(), userId, role), autoSave: true);
            }
        });
        return userId;
    }

    // Always on sale: one stage covering the whole day, in UTC, so the tests never depend on the current time of day.
    private static CreateUpdateSmartOfferDto AlwaysOnDeal(decimal price = 6m, int? quantity = 2, string titleEn = "Burger Meal")
    {
        var dto = new CreateUpdateSmartOfferDto
        {
            TitleAr = "برغر",
            TitleEn = titleEn,
            Strategy = SmartPricingStrategy.TimeBased,
            Currency = Currency.Usd,
            BasePrice = 10m,
            TimeZoneId = "UTC",
            IsEnabled = true,
        };

        dto.Stages.Add(new CreateUpdateSmartOfferStageDto
        {
            StartTime = "00:00",
            EndTime = "24:00",
            Price = price,
            Currency = Currency.Usd,
            QuantityLimit = quantity,
        });

        return dto;
    }

    private async Task<SmartOfferDto> CreateOfferAsync(Guid tenantId, CreateUpdateSmartOfferDto input)
    {
        using (_currentTenant.Change(tenantId))
        {
            return await WithUnitOfWorkAsync(() => _offerService.CreateAsync(input));
        }
    }

    private async Task<SmartOfferOrderDto> PlaceAsync(Guid customerId, Guid tenantId, Guid offerId, int quantity = 1)
    {
        using (LoginAs(customerId))
        {
            return await WithUnitOfWorkAsync(() => _customerService.PlaceOrderAsync(new PlaceSmartOfferOrderDto
            {
                TenantId = tenantId,
                SmartOfferId = offerId,
                Quantity = quantity,
            }));
        }
    }

    private async Task<CustomerSmartOfferDto> SingleVisibleOfferAsync(Guid customerId, Guid tenantId, Guid offerId)
    {
        using (LoginAs(customerId))
        {
            var list = await WithUnitOfWorkAsync(() => _customerService.GetOffersAsync(tenantId));
            return list.Items.Single(i => i.Id == offerId);
        }
    }

    [Fact]
    public async Task Owner_creation_quotes_the_live_price_and_the_stock_left_today()
    {
        var tenantId = await CreateTenantAsync();

        var offer = await CreateOfferAsync(tenantId, AlwaysOnDeal(price: 6m, quantity: 2));

        offer.Status.ShouldBe(SmartOfferStatus.Live);
        offer.CurrentPrice.ShouldBe(6m);
        offer.RemainingNow.ShouldBe(2);
        offer.Stages.Single().RemainingToday.ShouldBe(2);
        offer.Stages.Single().StartTime.ShouldBe("00:00");
        offer.Stages.Single().EndTime.ShouldBe("24:00");
    }

    [Fact]
    public async Task A_customer_order_is_priced_by_the_server_and_holds_stock()
    {
        var tenantId = await CreateTenantAsync();
        var customerId = Guid.NewGuid();
        await JoinAsync(tenantId, customerId);
        var offer = await CreateOfferAsync(tenantId, AlwaysOnDeal(price: 6m, quantity: 2));

        var order = await PlaceAsync(customerId, tenantId, offer.Id, quantity: 1);

        order.Status.ShouldBe(SmartOfferOrderStatus.Pending);
        order.UnitPrice.ShouldBe(6m);
        order.BasePrice.ShouldBe(10m);
        order.TotalAmount.ShouldBe(6m);
        order.Currency.ShouldBe(Currency.Usd);
        order.Code.Length.ShouldBe(SmartOfferConsts.CodeLength);
        order.ReservationExpiresAt.ShouldBeGreaterThan(order.PlacedAt);

        var visible = await SingleVisibleOfferAsync(customerId, tenantId, offer.Id);
        visible.RemainingNow.ShouldBe(1);
        visible.MyPendingOrder.ShouldNotBeNull();
        visible.MyPendingOrder!.Code.ShouldBe(order.Code);
        visible.DiscountPercent.ShouldBe(40);
    }

    [Fact]
    public async Task Ordering_the_same_deal_again_while_pending_returns_the_same_order()
    {
        var tenantId = await CreateTenantAsync();
        var customerId = Guid.NewGuid();
        await JoinAsync(tenantId, customerId);
        var offer = await CreateOfferAsync(tenantId, AlwaysOnDeal(quantity: 5));

        var first = await PlaceAsync(customerId, tenantId, offer.Id);
        var second = await PlaceAsync(customerId, tenantId, offer.Id);

        second.Id.ShouldBe(first.Id);
        var visible = await SingleVisibleOfferAsync(customerId, tenantId, offer.Id);
        visible.RemainingNow.ShouldBe(4);
    }

    [Fact]
    public async Task A_customer_cannot_take_more_than_is_left()
    {
        var tenantId = await CreateTenantAsync();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        await JoinAsync(tenantId, first);
        await JoinAsync(tenantId, second);
        var offer = await CreateOfferAsync(tenantId, AlwaysOnDeal(quantity: 2));

        await PlaceAsync(first, tenantId, offer.Id, quantity: 2);

        var error = await Should.ThrowAsync<UserFriendlyException>(() => PlaceAsync(second, tenantId, offer.Id, quantity: 1));
        error.Message.ShouldBe("This deal is sold out for now.");
    }

    [Fact]
    public async Task Cancelling_an_order_puts_its_stock_back_on_sale()
    {
        var tenantId = await CreateTenantAsync();
        var customerId = Guid.NewGuid();
        await JoinAsync(tenantId, customerId);
        var offer = await CreateOfferAsync(tenantId, AlwaysOnDeal(quantity: 1));
        var order = await PlaceAsync(customerId, tenantId, offer.Id);

        using (LoginAs(customerId))
        {
            var cancelled = await WithUnitOfWorkAsync(() => _customerService.CancelMyOrderAsync(tenantId, order.Id));
            cancelled.Status.ShouldBe(SmartOfferOrderStatus.Cancelled);
        }

        var visible = await SingleVisibleOfferAsync(customerId, tenantId, offer.Id);
        visible.RemainingNow.ShouldBe(1);
        visible.MyPendingOrder.ShouldBeNull();
    }

    [Fact]
    public async Task Only_a_joined_customer_can_place_an_order()
    {
        var tenantId = await CreateTenantAsync();
        var stranger = Guid.NewGuid();
        var offer = await CreateOfferAsync(tenantId, AlwaysOnDeal());

        await Should.ThrowAsync<UserFriendlyException>(() => PlaceAsync(stranger, tenantId, offer.Id));
    }

    [Fact]
    public async Task A_customer_can_watch_a_deals_price_once_and_stop_watching()
    {
        var tenantId = await CreateTenantAsync();
        var customerId = Guid.NewGuid();
        await JoinAsync(tenantId, customerId);
        var offer = await CreateOfferAsync(tenantId, AlwaysOnDeal(price: 6m, quantity: 2));

        using (LoginAs(customerId))
        {
            await WithUnitOfWorkAsync(() => _customerService.WatchPriceAsync(tenantId, offer.Id));
            await WithUnitOfWorkAsync(() => _customerService.WatchPriceAsync(tenantId, offer.Id)); // watching again changes nothing

            var watched = await WithUnitOfWorkAsync(() => _customerService.GetMyPriceWatchesAsync());
            watched.Count.ShouldBe(1);
            watched.Single().SmartOfferId.ShouldBe(offer.Id);
            watched.Single().TenantId.ShouldBe(tenantId);

            await WithUnitOfWorkAsync(() => _customerService.UnwatchPriceAsync(tenantId, offer.Id));
            (await WithUnitOfWorkAsync(() => _customerService.GetMyPriceWatchesAsync())).ShouldBeEmpty();
        }
    }

    [Fact]
    public async Task A_customer_who_has_not_joined_the_business_cannot_watch_its_deals()
    {
        var tenantId = await CreateTenantAsync();
        var stranger = Guid.NewGuid();
        var offer = await CreateOfferAsync(tenantId, AlwaysOnDeal(price: 6m, quantity: 2));

        using (LoginAs(stranger))
        {
            await Should.ThrowAsync<Volo.Abp.UserFriendlyException>(() => WithUnitOfWorkAsync(() => _customerService.WatchPriceAsync(tenantId, offer.Id)));
        }
    }

    [Fact]
    public async Task A_collected_deal_earns_the_business_points_rule_on_the_amount_paid()
    {
        var tenantId = await CreateTenantAsync();
        var customerId = Guid.NewGuid();
        var walletId = Guid.Empty;
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                var membership = Membership.Create(Guid.NewGuid(), customerId, DateTime.UtcNow);
                await _membershipRepository.InsertAsync(membership, autoSave: true);

                var wallet = PointsWallet.Create(Guid.NewGuid(), membership.Id);
                await _walletRepository.InsertAsync(wallet, autoSave: true);
                walletId = wallet.Id;

                await _pointRuleRepository.InsertAsync(PointRule.Create(Guid.NewGuid(), PointRuleType.PerCurrencyUnit, 2m, Currency.Usd), autoSave: true);
            }
        });

        var cashier = await CreateStaffAsync(tenantId, EmployeeRole.Cashier);
        var offer = await CreateOfferAsync(tenantId, AlwaysOnDeal(price: 6m, quantity: 2));
        var order = await PlaceAsync(customerId, tenantId, offer.Id, quantity: 2);

        using (_currentTenant.Change(tenantId))
        using (LoginAs(cashier))
        {
            await WithUnitOfWorkAsync(() => _staffService.CompleteAsync(new SmartOfferOrderCodeDto { Code = order.Code }));
        }

        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                var wallet = await _walletRepository.GetAsync(walletId);
                wallet.Balance.ShouldBe(24); // 2 units at 6 USD = 12 USD paid, at 2 points per dollar

                var earned = await _transactionRepository.GetListAsync(t => t.WalletId == walletId && t.Type == PointsTransactionType.Earn);
                earned.Single().Points.ShouldBe(24);
                earned.Single().ReferenceId.ShouldBe(order.Id);
            }
        });
    }

    [Fact]
    public async Task Staff_completion_turns_the_held_unit_into_a_sale()
    {
        var tenantId = await CreateTenantAsync();
        var customerId = Guid.NewGuid();
        await JoinAsync(tenantId, customerId);
        var cashier = await CreateStaffAsync(tenantId, EmployeeRole.Cashier);
        var offer = await CreateOfferAsync(tenantId, AlwaysOnDeal(quantity: 2));
        var order = await PlaceAsync(customerId, tenantId, offer.Id, quantity: 1);

        SmartOfferOrderStaffDto completed;
        using (_currentTenant.Change(tenantId))
        using (LoginAs(cashier))
        {
            completed = await WithUnitOfWorkAsync(() => _staffService.CompleteAsync(new SmartOfferOrderCodeDto { Code = order.Code }));
        }

        completed.Status.ShouldBe(SmartOfferOrderStatus.Completed);
        completed.CustomerName.ShouldBeNull(); // no profile created in this test; the lookup must still succeed
        var visible = await SingleVisibleOfferAsync(customerId, tenantId, offer.Id);
        visible.RemainingNow.ShouldBe(1);
    }

    [Fact]
    public async Task Staff_rejection_gives_the_unit_back_and_settles_the_order()
    {
        var tenantId = await CreateTenantAsync();
        var customerId = Guid.NewGuid();
        await JoinAsync(tenantId, customerId);
        var manager = await CreateStaffAsync(tenantId, EmployeeRole.BranchManager);
        var offer = await CreateOfferAsync(tenantId, AlwaysOnDeal(quantity: 1));
        var order = await PlaceAsync(customerId, tenantId, offer.Id);

        using (_currentTenant.Change(tenantId))
        using (LoginAs(manager))
        {
            var rejected = await WithUnitOfWorkAsync(() => _staffService.RejectAsync(new RejectSmartOfferOrderDto
            {
                Code = order.Code,
                Reason = "Kitchen closed",
            }));
            rejected.Status.ShouldBe(SmartOfferOrderStatus.Rejected);
            rejected.RejectionReason.ShouldBe("Kitchen closed");
        }

        var visible = await SingleVisibleOfferAsync(customerId, tenantId, offer.Id);
        visible.RemainingNow.ShouldBe(1);
    }

    [Fact]
    public async Task A_customer_cannot_act_as_the_counter()
    {
        var tenantId = await CreateTenantAsync();
        var customerId = Guid.NewGuid();
        await JoinAsync(tenantId, customerId);
        var offer = await CreateOfferAsync(tenantId, AlwaysOnDeal());
        var order = await PlaceAsync(customerId, tenantId, offer.Id);

        using (_currentTenant.Change(tenantId))
        using (LoginAs(customerId))
        {
            await Should.ThrowAsync<AbpAuthorizationException>(() =>
                WithUnitOfWorkAsync(() => _staffService.CompleteAsync(new SmartOfferOrderCodeDto { Code = order.Code })));
        }
    }

    [Fact]
    public async Task The_counter_code_lookup_ignores_case_spacing_and_hyphens()
    {
        var tenantId = await CreateTenantAsync();
        var customerId = Guid.NewGuid();
        await JoinAsync(tenantId, customerId);
        var cashier = await CreateStaffAsync(tenantId, EmployeeRole.Cashier);
        var offer = await CreateOfferAsync(tenantId, AlwaysOnDeal());
        var order = await PlaceAsync(customerId, tenantId, offer.Id);
        var typed = $"{order.Code[..4].ToLowerInvariant()}-{order.Code[4..]}";

        using (_currentTenant.Change(tenantId))
        using (LoginAs(cashier))
        {
            var found = await WithUnitOfWorkAsync(() => _staffService.LookupAsync(new SmartOfferOrderCodeDto { Code = typed }));
            found.Id.ShouldBe(order.Id);
        }
    }

    [Fact]
    public async Task A_fixed_price_deal_sells_its_daily_quantity_once_and_is_refused_afterwards()
    {
        var tenantId = await CreateTenantAsync();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        await JoinAsync(tenantId, first);
        await JoinAsync(tenantId, second);

        var fixedOffer = new CreateUpdateSmartOfferDto
        {
            TitleAr = "وجبة",
            TitleEn = "Fixed Meal",
            Strategy = SmartPricingStrategy.Fixed,
            Currency = Currency.Syp,
            BasePrice = 50000m,
            DailyQuantity = 1,
            TimeZoneId = "UTC",
            IsEnabled = true,
        };
        var offer = await CreateOfferAsync(tenantId, fixedOffer);
        offer.CurrentPrice.ShouldBe(50000m);

        await PlaceAsync(first, tenantId, offer.Id);

        var error = await Should.ThrowAsync<UserFriendlyException>(() => PlaceAsync(second, tenantId, offer.Id));
        error.Message.ShouldBe("This deal is sold out for now.");
    }

    [Fact]
    public async Task An_update_naming_a_stage_that_is_not_on_the_offer_is_refused()
    {
        var tenantId = await CreateTenantAsync();
        var offer = await CreateOfferAsync(tenantId, AlwaysOnDeal());

        var update = AlwaysOnDeal(price: 5m);
        update.Stages.Single().Id = Guid.NewGuid();

        await Should.ThrowAsync<UserFriendlyException>(() => WithinTenantAsync(tenantId, () =>
            WithUnitOfWorkAsync(() => _offerService.UpdateAsync(offer.Id, update))));
    }

    [Fact]
    public async Task Editing_a_stage_keeps_its_identity_so_pending_orders_stay_attached()
    {
        var tenantId = await CreateTenantAsync();
        var customerId = Guid.NewGuid();
        await JoinAsync(tenantId, customerId);
        var offer = await CreateOfferAsync(tenantId, AlwaysOnDeal(price: 6m, quantity: 3));
        var stageId = offer.Stages.Single().Id;
        var order = await PlaceAsync(customerId, tenantId, offer.Id);

        var edit = AlwaysOnDeal(price: 5m, quantity: 3);
        edit.Stages.Single().Id = stageId;
        var updated = await WithinTenantAsync(tenantId, () => WithUnitOfWorkAsync(() => _offerService.UpdateAsync(offer.Id, edit)));

        updated.Stages.Single().Id.ShouldBe(stageId);
        updated.CurrentPrice.ShouldBe(5m);

        // The order keeps the price it was quoted at, not the one the owner just set.
        using (LoginAs(customerId))
        {
            var stillMine = await WithUnitOfWorkAsync(() => _customerService.GetMyOrderAsync(tenantId, order.Id));
            stillMine.UnitPrice.ShouldBe(6m);
        }
    }

    [Fact]
    public async Task A_business_only_sees_its_own_deals_and_customers_of_another_business_see_none_of_them()
    {
        var tenantA = await CreateTenantAsync();
        var tenantB = await CreateTenantAsync();
        var customer = Guid.NewGuid();
        await JoinAsync(tenantB, customer);
        var offerA = await CreateOfferAsync(tenantA, AlwaysOnDeal(titleEn: "A-only deal"));

        using (LoginAs(customer))
        {
            var list = await WithUnitOfWorkAsync(() => _customerService.GetOffersAsync(tenantB));
            list.Items.ShouldNotContain(i => i.Id == offerA.Id);
        }
    }

    [Fact]
    public async Task The_counter_history_lists_settled_sales_and_leaves_open_orders_on_the_counter()
    {
        var tenantId = await CreateTenantAsync();
        var served = Guid.NewGuid();
        var waiting = Guid.NewGuid();
        await JoinAsync(tenantId, served);
        await JoinAsync(tenantId, waiting);
        var cashier = await CreateStaffAsync(tenantId, EmployeeRole.Cashier);
        var offer = await CreateOfferAsync(tenantId, AlwaysOnDeal(quantity: 5));
        var completed = await PlaceAsync(served, tenantId, offer.Id, quantity: 2);
        var pending = await PlaceAsync(waiting, tenantId, offer.Id);

        using (_currentTenant.Change(tenantId))
        using (LoginAs(cashier))
        {
            await WithUnitOfWorkAsync(() => _staffService.CompleteAsync(new SmartOfferOrderCodeDto { Code = completed.Code }));

            var history = await WithUnitOfWorkAsync(() => _staffService.GetHistoryAsync(new PagedAndSortedResultRequestDto { MaxResultCount = 10 }));

            history.Items.ShouldContain(i => i.Id == completed.Id && i.Status == SmartOfferOrderStatus.Completed && i.Quantity == 2);
            history.Items.ShouldNotContain(i => i.Id == pending.Id);
        }
    }

    [Fact]
    public async Task Completed_sales_are_listed_for_the_transactions_tab_and_open_or_refused_orders_are_not()
    {
        var tenantId = await CreateTenantAsync();
        var buyer = Guid.NewGuid();
        var refusedBuyer = Guid.NewGuid();
        var waitingBuyer = Guid.NewGuid();
        await JoinAsync(tenantId, buyer);
        await JoinAsync(tenantId, refusedBuyer);
        await JoinAsync(tenantId, waitingBuyer);
        var cashier = await CreateStaffAsync(tenantId, EmployeeRole.Cashier);
        var offerInput = AlwaysOnDeal(quantity: 10, titleEn: "Burger Meal");
        offerInput.DescriptionEn = "Burger, fries and a drink.";
        var offer = await CreateOfferAsync(tenantId, offerInput);
        var sold = await PlaceAsync(buyer, tenantId, offer.Id, quantity: 2);
        var refused = await PlaceAsync(refusedBuyer, tenantId, offer.Id);
        await PlaceAsync(waitingBuyer, tenantId, offer.Id);
        await CompleteOrderAsync(tenantId, cashier, sold.Code);

        using (_currentTenant.Change(tenantId))
        using (LoginAs(cashier))
        {
            await WithUnitOfWorkAsync(() => _staffService.RejectAsync(new RejectSmartOfferOrderDto
            {
                Code = refused.Code,
                Reason = "Out of stock",
            }));
        }

        // Another business's sale must never appear in this business's list.
        var otherTenantId = await CreateTenantAsync();
        var otherBuyer = Guid.NewGuid();
        await JoinAsync(otherTenantId, otherBuyer);
        var otherCashier = await CreateStaffAsync(otherTenantId, EmployeeRole.Cashier);
        var otherOffer = await CreateOfferAsync(otherTenantId, AlwaysOnDeal(quantity: 10));
        var otherOrder = await PlaceAsync(otherBuyer, otherTenantId, otherOffer.Id);
        await CompleteOrderAsync(otherTenantId, otherCashier, otherOrder.Code);

        var sales = await ListSalesAsync(tenantId, new SmartDealSaleFilterDto { MaxResultCount = 10 });

        sales.TotalCount.ShouldBe(1);
        var sale = sales.Items.Single();
        sale.Id.ShouldBe(sold.Id);
        sale.Code.ShouldBe(sold.Code);
        sale.Quantity.ShouldBe(2);
        sale.UnitPrice.ShouldBe(sold.UnitPrice);
        sale.BasePrice.ShouldBe(sold.BasePrice);
        sale.TotalAmount.ShouldBe(sold.TotalAmount);
        sale.PlacedAt.ShouldBe(sold.PlacedAt);
        sale.Currency.ShouldBe(Currency.Usd);
        sale.OfferTitleEn.ShouldBe("Burger Meal");
        sale.SmartOfferId.ShouldBe(offer.Id);
        sale.OfferDescriptionEn.ShouldBe("Burger, fries and a drink.");
        sale.CompletedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task The_sales_list_filters_by_staff_and_completion_date_and_pages_newest_first()
    {
        var tenantId = await CreateTenantAsync();
        var firstBuyer = Guid.NewGuid();
        var secondBuyer = Guid.NewGuid();
        await JoinAsync(tenantId, firstBuyer);
        await JoinAsync(tenantId, secondBuyer);
        var cashierA = await CreateStaffAsync(tenantId, EmployeeRole.Cashier);
        var cashierB = await CreateStaffAsync(tenantId, EmployeeRole.Cashier);
        var offer = await CreateOfferAsync(tenantId, AlwaysOnDeal(quantity: 10));
        var firstOrder = await PlaceAsync(firstBuyer, tenantId, offer.Id);
        var secondOrder = await PlaceAsync(secondBuyer, tenantId, offer.Id);
        await CompleteOrderAsync(tenantId, cashierA, firstOrder.Code);
        await CompleteOrderAsync(tenantId, cashierB, secondOrder.Code);

        var byStaff = await ListSalesAsync(tenantId, new SmartDealSaleFilterDto { StaffId = cashierA, MaxResultCount = 10 });
        byStaff.Items.Select(i => i.Id).ShouldBe(new[] { firstOrder.Id });

        var futureOnly = await ListSalesAsync(tenantId, new SmartDealSaleFilterDto
        {
            From = DateTime.UtcNow.AddDays(1),
            MaxResultCount = 10,
        });
        futureOnly.TotalCount.ShouldBe(0);

        var firstPage = await ListSalesAsync(tenantId, new SmartDealSaleFilterDto { MaxResultCount = 1 });
        firstPage.TotalCount.ShouldBe(2);
        firstPage.Items.Single().Id.ShouldBe(secondOrder.Id); // completed last, so shown first
    }

    [Fact]
    public async Task The_sales_search_finds_every_sale_of_a_deal_by_its_id_and_one_sale_by_its_code()
    {
        var tenantId = await CreateTenantAsync();
        var cashier = await CreateStaffAsync(tenantId, EmployeeRole.Cashier);
        var burger = await CreateOfferAsync(tenantId, AlwaysOnDeal(quantity: 10, titleEn: "Burger"));
        var fries = await CreateOfferAsync(tenantId, AlwaysOnDeal(quantity: 10, titleEn: "Fries"));

        var burgerSales = new List<SmartOfferOrderDto>();
        foreach (var _ in Enumerable.Range(0, 2))
        {
            var buyer = Guid.NewGuid();
            await JoinAsync(tenantId, buyer);
            var order = await PlaceAsync(buyer, tenantId, burger.Id);
            await CompleteOrderAsync(tenantId, cashier, order.Code);
            burgerSales.Add(order);
        }

        var friesBuyer = Guid.NewGuid();
        await JoinAsync(tenantId, friesBuyer);
        var friesSale = await PlaceAsync(friesBuyer, tenantId, fries.Id);
        await CompleteOrderAsync(tenantId, cashier, friesSale.Code);

        var byDeal = await ListSalesAsync(tenantId, new SmartDealSaleFilterDto { Search = burger.Id.ToString(), MaxResultCount = 10 });
        byDeal.TotalCount.ShouldBe(2);
        byDeal.Items.ShouldAllBe(sale => sale.SmartOfferId == burger.Id);

        // Typed the way the counter accepts a code: lower case, with a hyphen that is ignored.
        var typedCode = friesSale.Code.ToLowerInvariant();
        var byCode = await ListSalesAsync(tenantId, new SmartDealSaleFilterDto { Search = typedCode, MaxResultCount = 10 });
        byCode.Items.Select(sale => sale.Id).ShouldBe(new[] { friesSale.Id });

        var unknown = await ListSalesAsync(tenantId, new SmartDealSaleFilterDto { Search = "NOSUCHCODE", MaxResultCount = 10 });
        unknown.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task The_sales_list_can_be_narrowed_to_one_customers_purchases()
    {
        var tenantId = await CreateTenantAsync();
        var cashier = await CreateStaffAsync(tenantId, EmployeeRole.Cashier);
        var offer = await CreateOfferAsync(tenantId, AlwaysOnDeal(quantity: 10));
        var regular = Guid.NewGuid();
        var other = Guid.NewGuid();
        await JoinAsync(tenantId, regular);
        await JoinAsync(tenantId, other);

        var regularFirst = await PlaceAsync(regular, tenantId, offer.Id);
        await CompleteOrderAsync(tenantId, cashier, regularFirst.Code);
        var regularSecond = await PlaceAsync(regular, tenantId, offer.Id);
        await CompleteOrderAsync(tenantId, cashier, regularSecond.Code);
        var otherSale = await PlaceAsync(other, tenantId, offer.Id);
        await CompleteOrderAsync(tenantId, cashier, otherSale.Code);

        Guid regularMembershipId;
        using (_currentTenant.Change(tenantId))
        {
            regularMembershipId = (await WithUnitOfWorkAsync(() => _membershipRepository.FirstOrDefaultAsync(m => m.CustomerId == regular)))!.Id;
        }

        var mine = await ListSalesAsync(tenantId, new SmartDealSaleFilterDto { MembershipId = regularMembershipId, MaxResultCount = 10 });
        mine.TotalCount.ShouldBe(2);
        mine.Items.Select(sale => sale.Id).ShouldBe(new[] { regularSecond.Id, regularFirst.Id }, ignoreOrder: true);
    }

    [Fact]
    public async Task The_admin_customer_page_reads_a_customers_sales_in_the_business_they_were_made_in()
    {
        var tenantId = await CreateTenantAsync();
        var buyer = Guid.NewGuid();
        await JoinAsync(tenantId, buyer);
        var cashier = await CreateStaffAsync(tenantId, EmployeeRole.Cashier);
        var offer = await CreateOfferAsync(tenantId, AlwaysOnDeal(quantity: 10));
        var order = await PlaceAsync(buyer, tenantId, offer.Id);
        await CompleteOrderAsync(tenantId, cashier, order.Code);

        Guid membershipId;
        using (_currentTenant.Change(tenantId))
        {
            membershipId = (await WithUnitOfWorkAsync(() => _membershipRepository.FirstOrDefaultAsync(m => m.CustomerId == buyer)))!.Id;
        }

        var page = await WithUnitOfWorkAsync(() => _adminUserService.GetCustomerSmartDealSalesAsync(
            membershipId, tenantId, new PagedAndSortedResultRequestDto { MaxResultCount = 10 }));

        page.TotalCount.ShouldBe(1);
        page.Items.Single().Id.ShouldBe(order.Id);

        // Read against a different business, the same membership id finds nothing.
        var otherTenantId = await CreateTenantAsync();
        var elsewhere = await WithUnitOfWorkAsync(() => _adminUserService.GetCustomerSmartDealSalesAsync(
            membershipId, otherTenantId, new PagedAndSortedResultRequestDto { MaxResultCount = 10 }));
        elsewhere.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task A_suspended_business_takes_no_new_deal_orders_and_its_deals_are_not_listed()
    {
        var tenantId = await CreateTenantAsync();
        await ApproveBusinessAsync(tenantId, "Suspended Soon");
        var member = Guid.NewGuid();
        await JoinAsync(tenantId, member);
        var offer = await CreateOfferAsync(tenantId, AlwaysOnDeal(quantity: 10));
        await PlaceAsync(member, tenantId, offer.Id);

        await SuspendBusinessAsync(tenantId);

        // Already a member, but the business is suspended: no new hold, and the deal is no longer offered.
        var newBuyer = Guid.NewGuid();
        await JoinAsync(tenantId, newBuyer);
        await Should.ThrowAsync<UserFriendlyException>(() => PlaceAsync(newBuyer, tenantId, offer.Id));
        await Should.ThrowAsync<UserFriendlyException>(() => PlaceAsync(member, tenantId, offer.Id, quantity: 2));

        using (LoginAs(member))
        {
            var listed = await WithUnitOfWorkAsync(() => _customerService.GetOffersAsync(tenantId));
            listed.Items.ShouldBeEmpty();
        }
    }

    private async Task SuspendBusinessAsync(Guid tenantId)
    {
        var profiles = GetRequiredService<IRepository<BusinessProfile, Guid>>();
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                var profile = await profiles.FirstAsync();
                profile.Suspend();
                await profiles.UpdateAsync(profile, autoSave: true);
            }
        });
    }

    private async Task CompleteOrderAsync(Guid tenantId, Guid cashierId, string code)
    {
        using (_currentTenant.Change(tenantId))
        using (LoginAs(cashierId))
        {
            await WithUnitOfWorkAsync(() => _staffService.CompleteAsync(new SmartOfferOrderCodeDto { Code = code }));
        }
    }

    [Fact]
    public async Task The_sales_export_is_only_given_out_against_a_valid_token()
    {
        var tenantId = await CreateTenantAsync();
        var buyer = Guid.NewGuid();
        await JoinAsync(tenantId, buyer);
        var cashier = await CreateStaffAsync(tenantId, EmployeeRole.Cashier);
        var offer = await CreateOfferAsync(tenantId, AlwaysOnDeal(quantity: 10));
        var order = await PlaceAsync(buyer, tenantId, offer.Id);
        await CompleteOrderAsync(tenantId, cashier, order.Code);

        DownloadTokenResultDto token;
        using (_currentTenant.Change(tenantId))
        {
            token = await WithUnitOfWorkAsync(() => _reportsService.GetSmartDealSalesDownloadTokenAsync());
        }

        using (_currentTenant.Change(tenantId))
        {
            var file = await WithUnitOfWorkAsync(() => _reportsService.GetSmartDealSalesAsExcelFileAsync(
                new SmartDealSalesExcelDownloadDto { DownloadToken = token.Token }));
            file.GetStream().Length.ShouldBeGreaterThan(0);

            await Should.ThrowAsync<AbpAuthorizationException>(() => WithUnitOfWorkAsync(() => _reportsService.GetSmartDealSalesAsExcelFileAsync(
                new SmartDealSalesExcelDownloadDto { DownloadToken = "not-a-real-token" })));
        }

        // A token minted for one business cannot be redeemed for another's sales, even by someone holding it.
        var otherTenantId = await CreateTenantAsync();
        using (_currentTenant.Change(otherTenantId))
        {
            await Should.ThrowAsync<AbpAuthorizationException>(() => WithUnitOfWorkAsync(() => _reportsService.GetSmartDealSalesAsExcelFileAsync(
                new SmartDealSalesExcelDownloadDto { DownloadToken = token.Token })));
        }
    }

    private async Task<PagedResultDto<SmartDealSaleDto>> ListSalesAsync(Guid tenantId, SmartDealSaleFilterDto input)
    {
        using (_currentTenant.Change(tenantId))
        {
            return await WithUnitOfWorkAsync(() => _reportsService.GetSmartDealSalesAsync(input));
        }
    }

    [Fact]
    public async Task An_order_carries_its_deals_description_and_the_deal_still_opens_after_its_window()
    {
        var tenantId = await CreateTenantAsync();
        var customerId = Guid.NewGuid();
        await JoinAsync(tenantId, customerId);
        var offerInput = AlwaysOnDeal(quantity: 5, titleEn: "Family Box");
        offerInput.DescriptionEn = "Two burgers, fries and a drink.";
        var offer = await CreateOfferAsync(tenantId, offerInput);
        await PlaceAsync(customerId, tenantId, offer.Id);

        using (LoginAs(customerId))
        {
            var orders = await WithUnitOfWorkAsync(() => _customerService.GetMyOrdersAsync(new GetMySmartOfferOrdersInput()));
            orders.Items.Single().OfferDescriptionEn.ShouldBe("Two burgers, fries and a drink.");

            // The endpoint reads the deal itself, not the browse list, so it still answers once the sale window has ended.
            var details = await WithUnitOfWorkAsync(() => _customerService.GetOfferDetailsAsync(tenantId, offer.Id));
            details.TitleEn.ShouldBe("Family Box");
            details.DescriptionEn.ShouldBe("Two burgers, fries and a drink.");
            details.BasePrice.ShouldBe(10m);
        }
    }

    [Fact]
    public async Task A_customer_cannot_open_a_deal_they_have_not_ordered()
    {
        var tenantId = await CreateTenantAsync();
        var member = Guid.NewGuid();
        await JoinAsync(tenantId, member); // joined the business, but never ordered this deal
        var offer = await CreateOfferAsync(tenantId, AlwaysOnDeal());

        using (LoginAs(member))
        {
            await Should.ThrowAsync<EntityNotFoundException>(() => WithUnitOfWorkAsync(() => _customerService.GetOfferDetailsAsync(tenantId, offer.Id)));
        }
    }

    private async Task ApproveBusinessAsync(Guid tenantId, string displayName)
    {
        var profiles = GetRequiredService<IRepository<BusinessProfile, Guid>>();
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                var profile = BusinessProfile.Create(Guid.NewGuid());
                profile.SetDisplayName(displayName);
                profile.Approve();
                await profiles.InsertAsync(profile, autoSave: true);
            }
        });
    }

    [Fact]
    public async Task The_feed_shows_deals_from_approved_businesses_the_customer_joined_and_nothing_else()
    {
        var joined = await CreateTenantAsync();
        var stranger = await CreateTenantAsync();
        var pendingBusiness = await CreateTenantAsync();
        await ApproveBusinessAsync(joined, "Joined Cafe");
        await ApproveBusinessAsync(stranger, "Stranger Grill");

        var customer = Guid.NewGuid();
        await JoinAsync(joined, customer);
        await JoinAsync(pendingBusiness, customer); // joined, but the business is not approved yet

        var joinedDeal = await CreateOfferAsync(joined, AlwaysOnDeal(titleEn: "Joined deal"));
        var strangerDeal = await CreateOfferAsync(stranger, AlwaysOnDeal(titleEn: "Stranger deal"));
        var pendingDeal = await CreateOfferAsync(pendingBusiness, AlwaysOnDeal(titleEn: "Pending deal"));

        using (LoginAs(customer))
        {
            var feed = await WithUnitOfWorkAsync(() => _customerService.GetFeedAsync(50));

            feed.Items.ShouldContain(i => i.Id == joinedDeal.Id && i.BusinessName == "Joined Cafe" && i.IsMember);
            feed.Items.ShouldNotContain(i => i.Id == strangerDeal.Id);
            feed.Items.ShouldNotContain(i => i.Id == pendingDeal.Id);
        }
    }

    [Fact]
    public async Task The_order_history_spans_every_business_and_filters_on_the_server()
    {
        var first = await CreateTenantAsync();
        var second = await CreateTenantAsync();
        await ApproveBusinessAsync(first, "First Shop");
        await ApproveBusinessAsync(second, "Second Shop");

        var customer = Guid.NewGuid();
        await JoinAsync(first, customer);
        await JoinAsync(second, customer);
        var cashier = await CreateStaffAsync(first, EmployeeRole.Cashier);

        var firstDeal = await CreateOfferAsync(first, AlwaysOnDeal(quantity: 5, titleEn: "Shop one deal"));
        var secondDeal = await CreateOfferAsync(second, AlwaysOnDeal(quantity: 5, titleEn: "Shop two deal"));
        var otherSecondDeal = await CreateOfferAsync(second, AlwaysOnDeal(quantity: 5, titleEn: "Shop two other deal"));

        var completed = await PlaceAsync(customer, first, firstDeal.Id);
        using (_currentTenant.Change(first))
        using (LoginAs(cashier))
        {
            await WithUnitOfWorkAsync(() => _staffService.CompleteAsync(new SmartOfferOrderCodeDto { Code = completed.Code }));
        }

        var open = await PlaceAsync(customer, second, secondDeal.Id);
        var cancelled = await PlaceAsync(customer, second, otherSecondDeal.Id);
        using (LoginAs(customer))
        {
            await WithUnitOfWorkAsync(() => _customerService.CancelMyOrderAsync(second, cancelled.Id));
        }

        using (LoginAs(customer))
        {
            var all = await WithUnitOfWorkAsync(() => _customerService.GetMyOrdersAsync(new GetMySmartOfferOrdersInput { MaxResultCount = 20 }));
            all.TotalCount.ShouldBe(3);
            all.Items.ShouldContain(i => i.Id == completed.Id && i.BusinessName == "First Shop" && i.TenantId == first);
            all.Items.ShouldContain(i => i.Id == open.Id && i.BusinessName == "Second Shop");

            var active = await WithUnitOfWorkAsync(() => _customerService.GetMyOrdersAsync(
                new GetMySmartOfferOrdersInput { MaxResultCount = 20, Filter = CustomerDealOrderFilter.Active }));
            active.Items.Select(i => i.Id).ShouldBe(new[] { open.Id });

            var done = await WithUnitOfWorkAsync(() => _customerService.GetMyOrdersAsync(
                new GetMySmartOfferOrdersInput { MaxResultCount = 20, Filter = CustomerDealOrderFilter.Completed }));
            done.Items.Select(i => i.Id).ShouldBe(new[] { completed.Id });

            var closed = await WithUnitOfWorkAsync(() => _customerService.GetMyOrdersAsync(
                new GetMySmartOfferOrdersInput { MaxResultCount = 20, Filter = CustomerDealOrderFilter.Closed }));
            closed.Items.Select(i => i.Id).ShouldBe(new[] { cancelled.Id });
            closed.Items.Single().Status.ShouldBe(SmartOfferOrderStatus.Cancelled);
        }
    }

    [Fact]
    public async Task Ordering_a_different_quantity_while_an_order_is_open_is_refused_not_silently_ignored()
    {
        var tenantId = await CreateTenantAsync();
        var customerId = Guid.NewGuid();
        await JoinAsync(tenantId, customerId);
        var offer = await CreateOfferAsync(tenantId, AlwaysOnDeal(quantity: 5));

        await PlaceAsync(customerId, tenantId, offer.Id, quantity: 2);

        var error = await Should.ThrowAsync<UserFriendlyException>(() => PlaceAsync(customerId, tenantId, offer.Id, quantity: 1));
        error.Message.ShouldContain("already have 2");
    }

    private async Task<T> WithinTenantAsync<T>(Guid tenantId, Func<Task<T>> action)
    {
        using (_currentTenant.Change(tenantId))
        {
            return await action();
        }
    }
}
