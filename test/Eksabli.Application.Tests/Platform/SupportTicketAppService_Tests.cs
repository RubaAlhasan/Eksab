using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Eksabli.Notifications;
using Shouldly;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Volo.Abp.Security.Claims;
using Xunit;

namespace Eksabli.Platform;

public abstract class SupportTicketAppService_Tests<TStartupModule> : EksabliApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly ISupportTicketAppService _supportTicketAppService;
    private readonly ICurrentPrincipalAccessor _currentPrincipalAccessor;
    private readonly IRepository<UserNotification, Guid> _userNotificationRepository;

    protected SupportTicketAppService_Tests()
    {
        _supportTicketAppService = GetRequiredService<ISupportTicketAppService>();
        _currentPrincipalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
        _userNotificationRepository = GetRequiredService<IRepository<UserNotification, Guid>>();
    }

    private async Task<int> InboxCountAsync(Guid userId) =>
        (await _userNotificationRepository.GetListAsync(n => n.UserId == userId)).Count;

    private IDisposable LoginAs(Guid userId)
    {
        var identity = new ClaimsIdentity("Test");
        identity.AddClaim(new Claim(AbpClaimTypes.UserId, userId.ToString()));
        return _currentPrincipalAccessor.Change(new ClaimsPrincipal(identity));
    }

    [Fact]
    public async Task CreateAsync_Should_Create_A_Ticket_With_Its_First_Message()
    {
        var customerId = Guid.NewGuid();

        using (LoginAs(customerId))
        {
            var created = await WithUnitOfWorkAsync(() => _supportTicketAppService.CreateAsync(new CreateSupportTicketDto
            {
                Subject = "Can't redeem a coupon",
                Body = "The QR code isn't scanning at checkout.",
                Priority = SupportTicketPriority.High
            }));

            created.Status.ShouldBe(SupportTicketStatus.Open);
            created.CustomerId.ShouldBe(customerId);
            created.TenantId.ShouldBeNull();
            created.Messages.ShouldHaveSingleItem();
        }
    }

    [Fact]
    public async Task GetListAsync_Should_Include_A_Created_Ticket()
    {
        var customerId = Guid.NewGuid();
        Guid ticketId;

        using (LoginAs(customerId))
        {
            var created = await WithUnitOfWorkAsync(() => _supportTicketAppService.CreateAsync(new CreateSupportTicketDto
            {
                Subject = "Points didn't post",
                Body = "I bought coffee an hour ago and still see zero points."
            }));
            ticketId = created.Id;
        }

        var list = await WithUnitOfWorkAsync(() => _supportTicketAppService.GetListAsync(new SupportTicketFilterDto()));
        list.Items.ShouldContain(t => t.Id == ticketId);
    }

    [Fact]
    public async Task AddMessageAsync_Should_Move_Open_Ticket_To_InProgress()
    {
        var customerId = Guid.NewGuid();
        Guid ticketId;

        using (LoginAs(customerId))
        {
            var created = await WithUnitOfWorkAsync(() => _supportTicketAppService.CreateAsync(new CreateSupportTicketDto
            {
                Subject = "Referral bonus missing",
                Body = "My friend joined with my code but I never got the bonus."
            }));
            ticketId = created.Id;

            await WithUnitOfWorkAsync(() => _supportTicketAppService.AddMessageAsync(ticketId, new AddSupportTicketMessageDto
            {
                Body = "Any update on this?"
            }));
        }

        var ticket = await WithUnitOfWorkAsync(() => _supportTicketAppService.GetAsync(ticketId));
        ticket.Status.ShouldBe(SupportTicketStatus.InProgress);
        ticket.Messages.Count.ShouldBe(2);
    }

    [Fact]
    public async Task AddMessageAsync_Should_Notify_The_Customer_When_Someone_Else_Replies()
    {
        var customerId = Guid.NewGuid();
        var staffId = Guid.NewGuid();
        Guid ticketId;

        using (LoginAs(customerId))
        {
            var created = await WithUnitOfWorkAsync(() => _supportTicketAppService.CreateAsync(new CreateSupportTicketDto
            {
                Subject = "Can't redeem a coupon",
                Body = "The QR code isn't scanning at checkout.",
            }));
            ticketId = created.Id;
        }

        using (LoginAs(staffId))
        {
            await WithUnitOfWorkAsync(() => _supportTicketAppService.AddMessageAsync(ticketId, new AddSupportTicketMessageDto
            {
                Body = "We're looking into this — can you try again now?",
            }));
        }

        (await InboxCountAsync(customerId)).ShouldBe(1);
    }

    [Fact]
    public async Task AddMessageAsync_Should_Not_Notify_The_Customer_For_Their_Own_Follow_Up()
    {
        var customerId = Guid.NewGuid();
        Guid ticketId;

        using (LoginAs(customerId))
        {
            var created = await WithUnitOfWorkAsync(() => _supportTicketAppService.CreateAsync(new CreateSupportTicketDto
            {
                Subject = "Points didn't post",
                Body = "I bought coffee an hour ago and still see zero points.",
            }));
            ticketId = created.Id;

            await WithUnitOfWorkAsync(() => _supportTicketAppService.AddMessageAsync(ticketId, new AddSupportTicketMessageDto
            {
                Body = "Still nothing, any update?",
            }));
        }

        (await InboxCountAsync(customerId)).ShouldBe(0);
    }

    [Fact]
    public async Task ResolveAsync_Should_Transition_Status_To_Resolved()
    {
        Guid ticketId;

        using (LoginAs(Guid.NewGuid()))
        {
            var created = await WithUnitOfWorkAsync(() => _supportTicketAppService.CreateAsync(new CreateSupportTicketDto
            {
                Subject = "General question",
                Body = "How do tiers work?"
            }));
            ticketId = created.Id;
        }

        var resolved = await WithUnitOfWorkAsync(() => _supportTicketAppService.ResolveAsync(ticketId));
        resolved.Status.ShouldBe(SupportTicketStatus.Resolved);
    }
}
