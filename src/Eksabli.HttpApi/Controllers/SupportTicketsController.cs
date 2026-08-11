using System;
using System.Threading.Tasks;
using Eksabli.Permissions;
using Eksabli.Platform;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;

namespace Eksabli.Controllers;

[ApiController]
[Route("api/app/support-ticket")]
[Authorize]
public class SupportTicketsController : EksabliController
{
    private readonly ISupportTicketAppService _supportTicketAppService;

    public SupportTicketsController(ISupportTicketAppService supportTicketAppService)
    {
        _supportTicketAppService = supportTicketAppService;
    }

    [HttpPost]
    public Task<SupportTicketDto> CreateAsync(CreateSupportTicketDto input)
    {
        return _supportTicketAppService.CreateAsync(input);
    }

    [HttpGet("{id}")]
    public Task<SupportTicketDto> GetAsync(Guid id)
    {
        return _supportTicketAppService.GetAsync(id);
    }

    // No permission gate beyond the class-level [Authorize] — this is dual-purpose: a Support Agent
    // (Eksabli.SupportTickets.Manage) gets the full cross-tenant/cross-customer queue with whatever
    // filters they pass; anyone else gets silently self-scoped to their own tenant's or their own
    // customer tickets regardless of what they pass (see SupportTicketAppService.GetListAsync — the
    // scoping decision lives there, not here, so it can't be bypassed by calling this endpoint
    // directly with a crafted tenantId).
    [HttpGet]
    public Task<PagedResultDto<SupportTicketDto>> GetListAsync([FromQuery] SupportTicketFilterDto input)
    {
        return _supportTicketAppService.GetListAsync(input);
    }

    [HttpPost("{id}/messages")]
    public Task<SupportTicketMessageDto> AddMessageAsync(Guid id, AddSupportTicketMessageDto input)
    {
        return _supportTicketAppService.AddMessageAsync(id, input);
    }

    [Authorize(EksabliPermissions.SupportTickets.Manage)]
    [HttpPost("{id}/resolve")]
    public Task<SupportTicketDto> ResolveAsync(Guid id)
    {
        return _supportTicketAppService.ResolveAsync(id);
    }
}
