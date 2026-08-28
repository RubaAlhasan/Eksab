using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace Eksabli.Sms;

[RemoteService(IsEnabled = false)]
public class AdminSmsLogAppService : ApplicationService, IAdminSmsLogAppService
{
    private readonly IRepository<SmsLog, Guid> _smsLogRepository;

    public AdminSmsLogAppService(IRepository<SmsLog, Guid> smsLogRepository)
    {
        _smsLogRepository = smsLogRepository;
    }

    public async Task<PagedResultDto<SmsLogDto>> GetListAsync(AdminSmsLogFilterDto input)
    {
        // SmsLog carries no IMultiTenant — nothing to Disable<IMultiTenant>() for, unlike the
        // cross-tenant admin lists elsewhere in this app (AdminAuditLogAppService, AdminSubscriptionAppService).
        var queryable = await _smsLogRepository.GetQueryableAsync();

        if (!input.FilterText.IsNullOrWhiteSpace())
        {
            queryable = queryable.Where(x =>
                x.PhoneNumber.Contains(input.FilterText!) || x.Message.Contains(input.FilterText!));
        }

        var totalCount = await AsyncExecuter.CountAsync(queryable);

        var logs = await AsyncExecuter.ToListAsync(
            queryable
                .OrderByDescending(x => x.CreationTime)
                .Skip(input.SkipCount)
                .Take(input.MaxResultCount));

        return new PagedResultDto<SmsLogDto>(totalCount, ObjectMapper.Map<List<SmsLog>, List<SmsLogDto>>(logs));
    }

    public async Task ClearAsync()
    {
        await _smsLogRepository.DeleteDirectAsync(x => true);
    }
}
