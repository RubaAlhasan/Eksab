using System.Threading.Tasks;
using Eksabli.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eksabli.Controllers;

// The customer's own notification switches, shown in Settings. Applies across every business the customer belongs to.
[ApiController]
[Route("api/app/customer-notification-preferences")]
[Authorize]
public class CustomerNotificationPreferencesController : EksabliController
{
    private readonly ICustomerNotificationPreferenceAppService _service;

    public CustomerNotificationPreferencesController(ICustomerNotificationPreferenceAppService service)
    {
        _service = service;
    }

    [HttpGet("mine")]
    public Task<NotificationPreferencesDto> GetMyAsync()
    {
        return _service.GetMyAsync();
    }

    [HttpPut("mine")]
    public Task<NotificationPreferencesDto> UpdateMyAsync(NotificationPreferencesDto input)
    {
        return _service.UpdateMyAsync(input);
    }
}
