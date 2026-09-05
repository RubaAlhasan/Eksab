using Volo.Abp.Application.Dtos;

namespace Eksabli.Sms;

public class AdminSmsLogFilterDto : PagedAndSortedResultRequestDto
{
    // Matches on either PhoneNumber or Message — a phone number search finds "who got a code", a
    // free-text search finds the code itself if the admin already knows a few digits of it.
    public string? FilterText { get; set; }
}
