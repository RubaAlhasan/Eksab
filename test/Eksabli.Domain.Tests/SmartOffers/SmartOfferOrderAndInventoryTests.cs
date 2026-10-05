using System;
using Eksabli.Shared;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Domain.Entities;
using Xunit;

namespace Eksabli.SmartOffers;

public class SmartOfferOrderAndInventoryTests
{
    private static readonly DateTime Placed = new(2026, 10, 4, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly ServiceDay = new(2026, 10, 4);

    private static SmartPriceQuote Quote(DateTime windowEnd, int? limit = null, decimal price = 7m) => new(
        SlotId: Guid.NewGuid(),
        StageId: Guid.NewGuid(),
        Price: price,
        BasePrice: 10m,
        Currency: Currency.Usd,
        WindowStartUtc: windowEnd.AddHours(-2),
        WindowEndUtc: windowEnd,
        QuantityLimit: limit);

    private static SmartOfferOrder Place(SmartPriceQuote quote, int quantity = 1) => SmartOfferOrder.Place(
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        "3B02543F",
        "برغر",
        "Burger Meal",
        quote,
        quantity,
        ServiceDay,
        Placed);

    // ---- Reservation deadline ----------------------------------------------------------------------------------------

    [Fact]
    public void A_reservation_gets_the_standard_window_when_the_price_lasts_longer()
    {
        var deadline = SmartOfferOrder.ReservationDeadline(Placed, Quote(Placed.AddHours(3)));

        deadline.ShouldBe(Placed.AddMinutes(SmartOfferConsts.ReservationWindowMinutes));
    }

    [Fact]
    public void A_reservation_never_outlives_the_price_window_it_was_quoted_in()
    {
        // Quoted 5 minutes before the stage ends: the hold is cut to the stage's end, not the standard 15 minutes.
        var windowEnd = Placed.AddMinutes(5);

        SmartOfferOrder.ReservationDeadline(Placed, Quote(windowEnd)).ShouldBe(windowEnd);
    }

    // ---- Snapshot ----------------------------------------------------------------------------------------------------

    [Fact]
    public void An_order_snapshots_the_quoted_price_currency_and_total()
    {
        var quote = Quote(Placed.AddHours(3), price: 7m);

        var order = Place(quote, quantity: 3);

        order.UnitPrice.ShouldBe(7m);
        order.BasePrice.ShouldBe(10m);
        order.TotalAmount.ShouldBe(21m);
        order.Currency.ShouldBe(Currency.Usd);
        order.SlotId.ShouldBe(quote.SlotId);
        order.Status.ShouldBe(SmartOfferOrderStatus.Pending);
        order.HoldsStock.ShouldBeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(SmartOfferConsts.MaxOrderQuantity + 1)]
    public void An_order_quantity_outside_the_allowed_range_is_rejected(int quantity)
    {
        Should.Throw<UserFriendlyException>(() => Place(Quote(Placed.AddHours(3)), quantity));
    }

    // ---- Lifecycle ---------------------------------------------------------------------------------------------------

    [Fact]
    public void Complete_is_refused_once_the_reservation_has_lapsed()
    {
        var order = Place(Quote(Placed.AddHours(3)));

        Should.Throw<UserFriendlyException>(() => order.Complete(
            order.ReservationExpiresAt.AddSeconds(1),
            Guid.NewGuid(),
            null));

        order.Status.ShouldBe(SmartOfferOrderStatus.Pending);
    }

    [Fact]
    public void Complete_within_the_window_records_who_and_when()
    {
        var order = Place(Quote(Placed.AddHours(3)));
        var employee = Guid.NewGuid();
        var branch = Guid.NewGuid();
        var now = Placed.AddMinutes(4);

        order.Complete(now, employee, branch);

        order.Status.ShouldBe(SmartOfferOrderStatus.Completed);
        order.CompletedAt.ShouldBe(now);
        order.CompletedByEmployeeId.ShouldBe(employee);
        order.CompletedBranchId.ShouldBe(branch);
        order.HoldsStock.ShouldBeFalse();
    }

    [Fact]
    public void A_settled_order_cannot_be_completed_rejected_or_cancelled_again()
    {
        var order = Place(Quote(Placed.AddHours(3)));
        order.Complete(Placed.AddMinutes(1), Guid.NewGuid(), null);

        Should.Throw<UserFriendlyException>(() => order.Complete(Placed.AddMinutes(2), Guid.NewGuid(), null));
        Should.Throw<UserFriendlyException>(() => order.Reject(Guid.NewGuid(), "too late"));
        Should.Throw<UserFriendlyException>(() => order.Cancel());
    }

    [Fact]
    public void Reject_records_the_reason_and_the_employee()
    {
        var order = Place(Quote(Placed.AddHours(3)));
        var employee = Guid.NewGuid();

        order.Reject(employee, "Kitchen closed");

        order.Status.ShouldBe(SmartOfferOrderStatus.Rejected);
        order.RejectionReason.ShouldBe("Kitchen closed");
        order.CompletedByEmployeeId.ShouldBe(employee);
    }

    [Fact]
    public void Only_a_pending_order_can_be_marked_expired()
    {
        var pending = Place(Quote(Placed.AddHours(3)));
        pending.MarkExpired();
        pending.Status.ShouldBe(SmartOfferOrderStatus.Expired);

        var completed = Place(Quote(Placed.AddHours(3)));
        completed.Complete(Placed.AddMinutes(1), Guid.NewGuid(), null);
        completed.MarkExpired();
        completed.Status.ShouldBe(SmartOfferOrderStatus.Completed);
    }

    // ---- Inventory ---------------------------------------------------------------------------------------------------

    [Fact]
    public void Holds_within_capacity_succeed_and_report_what_remains()
    {
        var stock = SmartOfferInventory.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), ServiceDay);

        stock.Hold(3, capacity: 10);
        stock.Hold(2, capacity: 10);

        stock.Reserved.ShouldBe(5);
        stock.Remaining(10).ShouldBe(5);
    }

    [Fact]
    public void A_hold_beyond_what_remains_is_refused_with_the_count_left()
    {
        var stock = SmartOfferInventory.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), ServiceDay);
        stock.Hold(8, capacity: 10);

        var error = Should.Throw<UserFriendlyException>(() => stock.Hold(3, capacity: 10));

        error.Message.ShouldContain("Only 2 left");
        stock.Reserved.ShouldBe(8);
    }

    [Fact]
    public void A_sold_out_slot_says_so_rather_than_offering_a_quantity()
    {
        var stock = SmartOfferInventory.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), ServiceDay);
        stock.Hold(10, capacity: 10);

        var error = Should.Throw<UserFriendlyException>(() => stock.Hold(1, capacity: 10));

        error.Message.ShouldBe("This deal is sold out for now.");
    }

    [Fact]
    public void Unlimited_capacity_never_refuses_a_hold()
    {
        var stock = SmartOfferInventory.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), ServiceDay);

        stock.Hold(1_000, capacity: null);

        stock.Remaining(null).ShouldBeNull();
    }

    [Fact]
    public void Confirmed_units_are_sold_and_no_longer_held()
    {
        var stock = SmartOfferInventory.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), ServiceDay);
        stock.Hold(4, capacity: 10);

        stock.Confirm(3);

        stock.Reserved.ShouldBe(1);
        stock.Sold.ShouldBe(3);
        stock.Remaining(10).ShouldBe(6);
    }

    [Fact]
    public void Released_units_go_back_on_sale()
    {
        var stock = SmartOfferInventory.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), ServiceDay);
        stock.Hold(4, capacity: 10);

        stock.Release(4);

        stock.Reserved.ShouldBe(0);
        stock.Remaining(10).ShouldBe(10);
    }

    [Fact]
    public void Releasing_or_confirming_more_than_is_held_is_refused_rather_than_overselling()
    {
        var stock = SmartOfferInventory.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), ServiceDay);
        stock.Hold(1, capacity: 10);

        Should.Throw<AbpException>(() => stock.Release(2));
        Should.Throw<AbpException>(() => stock.Confirm(2));
    }

    [Fact]
    public void Remaining_never_goes_negative_when_the_owner_lowers_a_quota_below_what_sold()
    {
        var stock = SmartOfferInventory.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), ServiceDay);
        stock.Hold(4, capacity: 10);
        stock.Confirm(4);

        stock.Remaining(capacity: 3).ShouldBe(0);
    }
}
