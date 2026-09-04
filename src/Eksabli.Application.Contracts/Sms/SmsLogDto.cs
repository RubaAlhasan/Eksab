using System;
using Volo.Abp.Application.Dtos;

namespace Eksabli.Sms;

public class SmsLogDto : EntityDto<Guid>
{
    public string PhoneNumber { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public DateTime CreationTime { get; set; }
}
