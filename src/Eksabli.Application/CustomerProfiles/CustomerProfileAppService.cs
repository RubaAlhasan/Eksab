using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Users;

namespace Eksabli.CustomerProfiles;

public class CustomerProfileAppService : ApplicationService, ICustomerProfileAppService
{
    private readonly IRepository<CustomerProfile, Guid> _repository;

    public CustomerProfileAppService(IRepository<CustomerProfile, Guid> repository)
    {
        _repository = repository;
    }

    public async Task<CustomerProfileDto> GetMyProfileAsync()
    {
        var profile = await GetOrCreateAsync();
        return ObjectMapper.Map<CustomerProfile, CustomerProfileDto>(profile);
    }

    public async Task<CustomerProfileDto> UpdateMyProfileAsync(UpdateCustomerProfileDto input)
    {
        var profile = await GetOrCreateAsync();
        profile.SetName(input.FirstName, input.LastName);
        profile.SetDateOfBirth(input.DateOfBirth);
        profile.SetGender(input.Gender);
        await _repository.UpdateAsync(profile);
        return ObjectMapper.Map<CustomerProfile, CustomerProfileDto>(profile);
    }

    private async Task<CustomerProfile> GetOrCreateAsync()
    {
        var userId = CurrentUser.GetId();
        var profile = await _repository.FirstOrDefaultAsync(x => x.UserId == userId);
        if (profile == null)
        {
            // Should already exist from OTP first-login — defensive create-if-missing insurance.
            profile = CustomerProfile.Create(GuidGenerator.Create(), userId);
            await _repository.InsertAsync(profile);
        }

        return profile;
    }
}
