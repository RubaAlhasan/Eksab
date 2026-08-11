using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Eksabli.Permissions;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Users;

namespace Eksabli.Platform;

[RemoteService(IsEnabled = false)]
public class SupportTicketAppService : ApplicationService, ISupportTicketAppService
{
    private readonly ISupportTicketRepository _repository;
    private readonly IPermissionChecker _permissionChecker;

    public SupportTicketAppService(ISupportTicketRepository repository, IPermissionChecker permissionChecker)
    {
        _repository = repository;
        _permissionChecker = permissionChecker;
    }

    public async Task<SupportTicketDto> CreateAsync(CreateSupportTicketDto input)
    {
        var reporterId = CurrentUser.GetId();

        // Tenant-realm caller (business Owner/staff) -> TenantId set, CustomerId null.
        // Host-realm caller (customer) -> CustomerId set, TenantId null.
        var tenantId = CurrentTenant.Id;
        var customerId = tenantId == null ? reporterId : (Guid?)null;

        var ticket = SupportTicket.Create(
            GuidGenerator.Create(), tenantId, customerId, input.Subject, input.Priority,
            GuidGenerator.Create(), reporterId, input.Body, Clock.Now);
        await _repository.InsertAsync(ticket);

        return MapWithMessages(ticket);
    }

    public async Task<SupportTicketDto> GetAsync(Guid id)
    {
        var ticket = await _repository.GetAsync(id);
        await EnsureCanAccessAsync(ticket);
        return MapWithMessages(ticket);
    }

    // Two callers share this one action (see the controller's own comment for why there's no separate
    // "my tickets" endpoint): a Support Agent sees the real cross-tenant/cross-customer queue, exactly
    // as `input` asks for it. Everyone else — tenant-realm business staff, or a Host-realm customer —
    // gets forcibly scoped to their own tickets, the *caller's* identity always wins over whatever
    // `input.TenantId` says, so this can never become an accidental cross-tenant read no matter what a
    // non-Manage caller passes.
    public async Task<PagedResultDto<SupportTicketDto>> GetListAsync(SupportTicketFilterDto input)
    {
        var tenantId = input.TenantId;
        Guid? customerId = null;

        if (!await _permissionChecker.IsGrantedAsync(EksabliPermissions.SupportTickets.Manage))
        {
            tenantId = CurrentTenant.Id;
            customerId = CurrentTenant.Id == null ? CurrentUser.GetId() : (Guid?)null;
        }

        var (tickets, totalCount) = await _repository.GetListAsync(
            status: input.Status,
            priority: input.Priority,
            tenantId: tenantId,
            customerId: customerId,
            sorting: input.Sorting,
            skipCount: input.SkipCount,
            maxResultCount: input.MaxResultCount);

        return new PagedResultDto<SupportTicketDto>(totalCount, ObjectMapper.Map<List<SupportTicket>, List<SupportTicketDto>>(tickets));
    }

    public async Task<SupportTicketMessageDto> AddMessageAsync(Guid id, AddSupportTicketMessageDto input)
    {
        var ticket = await _repository.GetAsync(id);
        await EnsureCanAccessAsync(ticket);

        var message = ticket.AddMessage(GuidGenerator.Create(), CurrentUser.GetId(), input.Body, Clock.Now);
        await _repository.UpdateAsync(ticket);

        return ObjectMapper.Map<SupportTicketMessage, SupportTicketMessageDto>(message);
    }

    public async Task<SupportTicketDto> ResolveAsync(Guid id)
    {
        var ticket = await _repository.GetAsync(id);
        ticket.Resolve();
        await _repository.UpdateAsync(ticket);
        return MapWithMessages(ticket);
    }

    // Support Agents (Eksabli.SupportTickets.Manage) see every ticket; everyone else only their own
    // (matched by ambient tenant for a business's ticket, or by CurrentUser for a customer's ticket).
    private async Task EnsureCanAccessAsync(SupportTicket ticket)
    {
        if (await _permissionChecker.IsGrantedAsync(EksabliPermissions.SupportTickets.Manage))
        {
            return;
        }

        var isOwnTenantTicket = ticket.TenantId.HasValue && ticket.TenantId == CurrentTenant.Id;
        var isOwnCustomerTicket = ticket.CustomerId.HasValue && ticket.CustomerId == CurrentUser.GetId();

        if (!isOwnTenantTicket && !isOwnCustomerTicket)
        {
            throw new AbpAuthorizationException("You don't have access to this ticket.");
        }
    }

    private SupportTicketDto MapWithMessages(SupportTicket ticket)
    {
        var dto = ObjectMapper.Map<SupportTicket, SupportTicketDto>(ticket);
        dto.Messages = ticket.Messages
            .Select(m => ObjectMapper.Map<SupportTicketMessage, SupportTicketMessageDto>(m))
            .ToList();
        return dto;
    }
}
