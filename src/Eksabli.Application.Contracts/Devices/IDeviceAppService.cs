using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;

namespace Eksabli.Devices;

// Exposed via an explicit controller (src/Eksabli.HttpApi/Controllers/DevicesController.cs).
[RemoteService(IsEnabled = false)]
public interface IDeviceAppService : IApplicationService
{
    Task<DeviceDto> RegisterAsync(RegisterDeviceDto input);

    Task<List<DeviceDto>> GetListAsync();

    Task RemoveAsync(Guid id);
}
