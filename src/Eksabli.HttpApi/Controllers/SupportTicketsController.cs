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

    // The queue — every ticket, not just the caller's own, so this specifically needs the Support
    // Agent permission on top of the class-level authenticated requirement.
    [Authorize(EksabliPermissions.SupportTickets.Manage)]
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
