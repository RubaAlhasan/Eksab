using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace Eksabli.EmployeeAssignments;

[RemoteService(IsEnabled = false)]
public class EmployeeAssignmentAppService : ApplicationService, IEmployeeAssignmentAppService
{
    private readonly IEmployeeAssignmentRepository _repository;
    private readonly IdentityUserManager _identityUserManager;
    private readonly IIdentityUserRepository _identityUserRepository;

    public EmployeeAssignmentAppService(
        IEmployeeAssignmentRepository repository,
        IdentityUserManager identityUserManager,
        IIdentityUserRepository identityUserRepository)
    {
        _repository = repository;
        _identityUserManager = identityUserManager;
        _identityUserRepository = identityUserRepository;
    }

    public async Task<PagedResultDto<EmployeeAssignmentDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var (assignments, totalCount) = await _repository.GetListAsync(
            sorting: input.Sorting,
            skipCount: input.SkipCount,
            maxResultCount: input.MaxResultCount);

        var dtos = ObjectMapper.Map<List<EmployeeAssignment>, List<EmployeeAssignmentDto>>(assignments);
        await SetUserEmailsAsync(dtos);

        return new PagedResultDto<EmployeeAssignmentDto>(totalCount, dtos);
    }

    public async Task<InviteEmployeeResultDto> InviteAsync(InviteEmployeeDto input)
    {
        var existingUser = await _identityUserRepository.FindByNormalizedUserNameAsync(input.Email.ToUpperInvariant());
        if (existingUser != null)
        {
            throw new UserFriendlyException($"An employee with email '{input.Email}' has already been invited.");
        }

        var tempPassword = $"{Guid.NewGuid():N}Aa1!";

        // No email is sent anywhere in this codebase (no SMTP sender configured) — this temp password is
        // the ONLY way the invited person can ever get in, so it's handed back to the caller once, here,
        // and never persisted/logged anywhere else. ShouldChangePasswordOnNextLogin (a real, built-in
        // IdentityUser column — confirmed in the EF model) means it can't quietly become their permanent
        // password either; ABP's own change-password flow clears this flag once they set a real one.
        var user = new IdentityUser(GuidGenerator.Create(), input.Email, input.Email, CurrentTenant.Id);
        user.SetShouldChangePasswordOnNextLogin(true);
        (await _identityUserManager.CreateAsync(user, tempPassword)).CheckErrors();

        var assignment = EmployeeAssignment.Create(GuidGenerator.Create(), user.Id, input.Role, input.BranchId);
        await _repository.InsertAsync(assignment);

        var dto = ObjectMapper.Map<EmployeeAssignment, EmployeeAssignmentDto>(assignment);
        dto.UserEmail = user.Email;
        return new InviteEmployeeResultDto { Assignment = dto, TemporaryPassword = tempPassword };
    }

    public async Task<EmployeeAssignmentDto> UpdateAsync(Guid id, UpdateEmployeeAssignmentDto input)
    {
        var assignment = await _repository.GetAsync(id);
        assignment.ChangeRole(input.Role);
        assignment.ReassignBranch(input.BranchId);
        await _repository.UpdateAsync(assignment);

        var dto = ObjectMapper.Map<EmployeeAssignment, EmployeeAssignmentDto>(assignment);
        var user = await _identityUserRepository.FindAsync(assignment.UserId);
        dto.UserEmail = user?.Email;
        return dto;
    }

    public async Task RemoveAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }

    private async Task SetUserEmailsAsync(List<EmployeeAssignmentDto> dtos)
    {
        foreach (var dto in dtos)
        {
            var user = await _identityUserRepository.FindAsync(dto.UserId);
            dto.UserEmail = user?.Email;
        }
    }
}
