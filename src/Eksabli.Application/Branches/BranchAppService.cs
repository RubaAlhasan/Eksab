using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;

namespace Eksabli.Branches;

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
