using System;
using Eksabli.Platform;
using Shouldly;
using Volo.Abp;
using Xunit;

namespace Eksabli.Platform;

public class SupportTicketTests
{
    private static SupportTicket CreateTicket(SupportTicketPriority priority = SupportTicketPriority.High)
    {
        return SupportTicket.Create(
            Guid.NewGuid(), Guid.NewGuid(), null, "Can't redeem a coupon", priority,
            Guid.NewGuid(), Guid.NewGuid(), "The QR code isn't scanning at checkout.", DateTime.UtcNow);
    }

    [Fact]
    public void Create_Should_Start_Open_With_Its_Initial_Message()
    {
        var ticket = CreateTicket();

        ticket.Status.ShouldBe(SupportTicketStatus.Open);
        ticket.Messages.ShouldHaveSingleItem();
    }

    [Fact]
    public void AddMessage_Should_Transition_Open_To_InProgress()
    {
        var ticket = CreateTicket();

        ticket.AddMessage(Guid.NewGuid(), Guid.NewGuid(), "Looking into it", DateTime.UtcNow);

        ticket.Status.ShouldBe(SupportTicketStatus.InProgress);
        ticket.Messages.Count.ShouldBe(2);
    }

    [Fact]
    public void AddMessage_Should_Throw_When_Closed()
    {
        var ticket = CreateTicket();
        ticket.Close();

        Should.Throw<UserFriendlyException>(() => ticket.AddMessage(Guid.NewGuid(), Guid.NewGuid(), "Still there?", DateTime.UtcNow));
    }

    [Fact]
    public void Resolve_Should_Throw_When_Already_Closed()
    {
        var ticket = CreateTicket();
        ticket.Close();

        Should.Throw<UserFriendlyException>(() => ticket.Resolve());
    }

    [Fact]
    public void Reopen_Should_Throw_When_Not_Resolved()
    {
        var ticket = CreateTicket();

        Should.Throw<UserFriendlyException>(() => ticket.Reopen());
    }

    [Fact]
    public void Reopen_Should_Transition_Resolved_To_InProgress()
    {
        var ticket = CreateTicket();
        ticket.Resolve();

        ticket.Reopen();

        ticket.Status.ShouldBe(SupportTicketStatus.InProgress);
    }
}
