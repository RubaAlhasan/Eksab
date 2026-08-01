using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Eksabli.Devices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Eksabli.Controllers;

[ApiController]
[Route("api/app/devices")]
[Authorize]
public class DevicesController : EksabliController
{
    private readonly IDeviceAppService _deviceAppService;

    public DevicesController(IDeviceAppService deviceAppService)
    {
        _deviceAppService = deviceAppService;
    }

    [HttpPost]
    public Task<DeviceDto> RegisterAsync(RegisterDeviceDto input)
    {
        return _deviceAppService.RegisterAsync(input);
    }

    [HttpGet]
    public Task<List<DeviceDto>> GetListAsync()
    {
        return _deviceAppService.GetListAsync();
    }

    [HttpDelete("{id}")]
    public Task RemoveAsync(Guid id)
    {
        return _deviceAppService.RemoveAsync(id);
    }
}
