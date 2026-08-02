using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp.Modularity;
using Volo.Abp.Security.Claims;
using Xunit;

namespace Eksabli.Platform;

public abstract class SupportTicketAppService_Tests<TStartupModule> : EksabliApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly ISupportTicketAppService _supportTicketAppService;
    private readonly ICurrentPrincipalAccessor _currentPrincipalAccessor;

    protected SupportTicketAppService_Tests()
    {
        _supportTicketAppService = GetRequiredService<ISupportTicketAppService>();
        _currentPrincipalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
    }

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
