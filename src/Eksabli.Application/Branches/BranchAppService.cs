using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;
using Eksabli.Features;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Features;

namespace Eksabli.Branches;

[RemoteService(IsEnabled = false)]
public class BranchAppService : ApplicationService, IBranchAppService
{
    private readonly IBranchRepository _repository;

    public BranchAppService(IBranchRepository repository)
    {
        _repository = repository;
    }

    public async Task<BranchDto> GetAsync(Guid id)
    {
        var branch = await _repository.GetAsync(id);
        return ObjectMapper.Map<Branch, BranchDto>(branch);
    }

    public async Task<PagedResultDto<BranchDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var (branches, totalCount) = await _repository.GetListAsync(
            sorting: input.Sorting,
            skipCount: input.SkipCount,
            maxResultCount: input.MaxResultCount);

        return new PagedResultDto<BranchDto>(
            totalCount,
            ObjectMapper.Map<List<Branch>, List<BranchDto>>(branches)
        );
    }

    public async Task<BranchDto> CreateAsync(CreateUpdateBranchDto input)
    {
        // Plan-limit enforcement via ABP Feature Management, not new business logic — see
        // docs/eksabli-loyalty-platform/features/04-billing-subscriptions/README.md.
        var maxBranches = await FeatureChecker.GetAsync<int>(EksabliFeatures.MaxBranches);
        var currentCount = await _repository.GetCountAsync();
        if (currentCount >= maxBranches)
        {
            throw new UserFriendlyException("You've reached the branch limit for your current plan. Upgrade to add more branches.");
        }

        var branch = Branch.Create(GuidGenerator.Create(), input.Name);
        ApplyInput(branch, input);
        await _repository.InsertAsync(branch);
        return ObjectMapper.Map<Branch, BranchDto>(branch);
    }

    public async Task<BranchDto> UpdateAsync(Guid id, CreateUpdateBranchDto input)
    {
        var branch = await _repository.GetAsync(id);
        branch.SetName(input.Name);
        ApplyInput(branch, input);
        await _repository.UpdateAsync(branch);
        return ObjectMapper.Map<Branch, BranchDto>(branch);
    }

    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }

    private static void ApplyInput(Branch branch, CreateUpdateBranchDto input)
    {
        branch.SetAddress(input.Address);
        branch.SetLocation(input.Latitude, input.Longitude);
        branch.SetPhone(input.Phone);
        branch.SetOpeningHours(input.OpeningHoursJson);
    }
}
