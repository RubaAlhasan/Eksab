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
using Volo.Abp.PermissionManagement;

namespace Eksabli.EmployeeAssignments;

[RemoteService(IsEnabled = false)]
public class EmployeeAssignmentAppService : ApplicationService, IEmployeeAssignmentAppService
{
    private readonly IEmployeeAssignmentRepository _repository;
    private readonly IdentityUserManager _identityUserManager;
    private readonly IIdentityUserRepository _identityUserRepository;
    private readonly IdentityRoleManager _identityRoleManager;
    private readonly IPermissionDataSeeder _permissionDataSeeder;

    public EmployeeAssignmentAppService(
        IEmployeeAssignmentRepository repository,
        IdentityUserManager identityUserManager,
        IIdentityUserRepository identityUserRepository,
        IdentityRoleManager identityRoleManager,
        IPermissionDataSeeder permissionDataSeeder)
    {
        _repository = repository;
        _identityUserManager = identityUserManager;
        _identityUserRepository = identityUserRepository;
        _identityRoleManager = identityRoleManager;
        _permissionDataSeeder = permissionDataSeeder;
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

        // Without this, an invited employee got a real login but no ABP role/permission grant at
        // all — every permission-gated Business Portal endpoint (Members, Rewards, Campaigns,
        // Branches, ...) was silently unreachable to them, leaving only whatever PosAppService
        // .CheckStaffRoleAsync checks directly against EmployeeAssignment.Role. See
        // EmployeeRolePermissionDefaults's own comment for the tier -> role/permissions mapping.
        await EnsureTierRoleAsync(input.Role);
        (await _identityUserManager.AddToRoleAsync(user, EmployeeRolePermissionDefaults.RoleName(input.Role))).CheckErrors();

        var assignment = EmployeeAssignment.Create(GuidGenerator.Create(), user.Id, input.Role, input.BranchId);
        await _repository.InsertAsync(assignment);

        var dto = ObjectMapper.Map<EmployeeAssignment, EmployeeAssignmentDto>(assignment);
        dto.UserEmail = user.Email;
        return new InviteEmployeeResultDto { Assignment = dto, TemporaryPassword = tempPassword };
    }

    public async Task<EmployeeAssignmentDto> UpdateAsync(Guid id, UpdateEmployeeAssignmentDto input)
    {
        var assignment = await _repository.GetAsync(id);
        var oldRole = assignment.Role;

        assignment.ChangeRole(input.Role);
        assignment.ReassignBranch(input.BranchId);
        await _repository.UpdateAsync(assignment);

        var user = await _identityUserRepository.FindAsync(assignment.UserId);
        if (user != null && oldRole != input.Role)
        {
            await MoveUserBetweenTierRolesAsync(user, oldRole, input.Role);
        }

        var dto = ObjectMapper.Map<EmployeeAssignment, EmployeeAssignmentDto>(assignment);
        dto.UserEmail = user?.Email;
        return dto;
    }

    public async Task RemoveAsync(Guid id)
    {
        var assignment = await _repository.GetAsync(id);

        // A removed employee keeps their real IdentityUser login (unchanged, pre-existing behavior)
        // but must not silently keep whatever Business Portal access their tier role granted.
        var user = await _identityUserRepository.FindAsync(assignment.UserId);
        if (user != null)
        {
            var roleName = EmployeeRolePermissionDefaults.RoleName(assignment.Role);
            if (await _identityUserManager.IsInRoleAsync(user, roleName))
            {
                (await _identityUserManager.RemoveFromRoleAsync(user, roleName)).CheckErrors();
            }
        }

        await _repository.DeleteAsync(id);
    }

    // Creates this tier's ABP Identity Role (tenant-scoped) the first time it's ever needed, seeded
    // with EmployeeRolePermissionDefaults' preset permissions — a no-op after that first call, and a
    // complete no-op for Owner (maps to the tenant's pre-existing "admin" role, never re-provisioned).
    private async Task EnsureTierRoleAsync(EmployeeRole role)
    {
        if (role == EmployeeRole.Owner)
        {
            return;
        }

        var roleName = EmployeeRolePermissionDefaults.RoleName(role);
        if (await _identityRoleManager.FindByNameAsync(roleName) != null)
        {
            return;
        }

        var identityRole = new IdentityRole(GuidGenerator.Create(), roleName, CurrentTenant.Id);
        (await _identityRoleManager.CreateAsync(identityRole)).CheckErrors();

        var defaultPermissions = EmployeeRolePermissionDefaults.DefaultPermissions(role);
        if (defaultPermissions.Length > 0)
        {
            // "R" — Volo.Abp.PermissionManagement.RolePermissionValueProvider.ProviderName's own
            // value. That type lives in Volo.Abp.PermissionManagement.Domain.Identity, which this
            // (Application) layer doesn't reference (only Eksabli.Domain does, for
            // AdminPermissionDataSeederContributor) — not worth a new package reference just for
            // this one well-known, stable literal.
            await _permissionDataSeeder.SeedAsync("R", roleName, defaultPermissions, CurrentTenant.Id);
        }
    }

    private async Task MoveUserBetweenTierRolesAsync(IdentityUser user, EmployeeRole oldRole, EmployeeRole newRole)
    {
        var oldRoleName = EmployeeRolePermissionDefaults.RoleName(oldRole);
        if (await _identityUserManager.IsInRoleAsync(user, oldRoleName))
        {
            (await _identityUserManager.RemoveFromRoleAsync(user, oldRoleName)).CheckErrors();
        }

        await EnsureTierRoleAsync(newRole);
        var newRoleName = EmployeeRolePermissionDefaults.RoleName(newRole);
        if (!await _identityUserManager.IsInRoleAsync(user, newRoleName))
        {
            (await _identityUserManager.AddToRoleAsync(user, newRoleName)).CheckErrors();
        }
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
