using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Eksabli.Platform;

// Exposed via an explicit controller (src/Eksabli.HttpApi/Controllers/SupportTicketsController.cs).
// CreateAsync/GetMyTicketsAsync are reporter-initiated (any authenticated user, tenant or Host realm);
// GetListAsync (queue)/AddMessageAsync (thread)/ResolveAsync are the Support Agent tooling gated by
// Eksabli.SupportTickets.Manage.
[RemoteService(IsEnabled = false)]
public interface ISupportTicketAppService : IApplicationService
{
    Task<SupportTicketDto> CreateAsync(CreateSupportTicketDto input);

    Task<SupportTicketDto> GetAsync(Guid id);

    Task<PagedResultDto<SupportTicketDto>> GetListAsync(SupportTicketFilterDto input);

    Task<SupportTicketMessageDto> AddMessageAsync(Guid id, AddSupportTicketMessageDto input);

    Task<SupportTicketDto> ResolveAsync(Guid id);
}
