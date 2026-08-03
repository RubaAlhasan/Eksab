using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Users;

namespace Eksabli.Devices;

[RemoteService(IsEnabled = false)]
public class DeviceAppService : ApplicationService, IDeviceAppService
{
    private readonly IRepository<Device, Guid> _repository;

    public DeviceAppService(IRepository<Device, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<DeviceDto> RegisterAsync(RegisterDeviceDto input)
    {
        var userId = CurrentUser.GetId();
        var device = await _repository.FirstOrDefaultAsync(x => x.PushToken == input.PushToken);

        if (device == null)
        {
            device = Device.Create(GuidGenerator.Create(), userId, input.Platform);
            device.UpdatePushToken(input.PushToken);
            device.Touch(input.AppVersion);
            await _repository.InsertAsync(device);
        }
        else
        {
            device.Touch(input.AppVersion);
            await _repository.UpdateAsync(device);
        }

        return ObjectMapper.Map<Device, DeviceDto>(device);
    }

    public async Task<List<DeviceDto>> GetListAsync()
    {
        var userId = CurrentUser.GetId();
        var devices = await _repository.GetListAsync(x => x.CustomerId == userId);
        return ObjectMapper.Map<List<Device>, List<DeviceDto>>(devices);
    }

    public async Task RemoveAsync(Guid id)
    {
        var userId = CurrentUser.GetId();
        var device = await _repository.GetAsync(id);
        if (device.CustomerId != userId)
        {
            throw new AbpAuthorizationException("You can only remove your own devices.");
        }

        await _repository.DeleteAsync(device);
    }
}
