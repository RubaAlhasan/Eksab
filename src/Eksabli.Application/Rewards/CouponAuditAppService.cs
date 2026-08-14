using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;
using Eksabli.Branches;
using Eksabli.Shared;
using Microsoft.Extensions.Caching.Distributed;
using MiniExcelLibs;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Caching;
using Volo.Abp.Content;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace Eksabli.Rewards;

[RemoteService(IsEnabled = false)]
public class CouponAuditAppService : ApplicationService, ICouponAuditAppService
{
    private readonly ICouponRepository _couponRepository;
    private readonly IRewardRepository _rewardRepository;
    private readonly IRepository<Branch, Guid> _branchRepository;
    private readonly IIdentityUserRepository _identityUserRepository;
    private readonly IDistributedCache<CouponExcelDownloadTokenCacheItem, string> _excelDownloadTokenCache;

    public CouponAuditAppService(
        ICouponRepository couponRepository,
        IRewardRepository rewardRepository,
        IRepository<Branch, Guid> branchRepository,
        IIdentityUserRepository identityUserRepository,
        IDistributedCache<CouponExcelDownloadTokenCacheItem, string> excelDownloadTokenCache)
    {
        _couponRepository = couponRepository;
        _rewardRepository = rewardRepository;
        _branchRepository = branchRepository;
        _identityUserRepository = identityUserRepository;
        _excelDownloadTokenCache = excelDownloadTokenCache;
    }

    public async Task<PagedResultDto<CouponDto>> GetListAsync(CouponAuditFilterDto input)
    {
        var (coupons, totalCount) = await _couponRepository.GetListAsync(
            status: input.Status,
            branchId: input.BranchId,
            membershipId: input.MembershipId,
            sorting: input.Sorting,
            skipCount: input.SkipCount,
            maxResultCount: input.MaxResultCount);

        var dtos = ObjectMapper.Map<List<Coupon>, List<CouponDto>>(coupons);
        await SetRewardNamesAsync(dtos);
        return new PagedResultDto<CouponDto>(totalCount, dtos);
    }

    public async Task<IRemoteStreamContent> GetListAsExcelFileAsync(CouponExcelDownloadDto input)
    {
        var downloadToken = await _excelDownloadTokenCache.GetAsync(input.DownloadToken);
        if (downloadToken == null || input.DownloadToken != downloadToken.Token)
        {
            throw new AbpAuthorizationException("Invalid download token: " + input.DownloadToken);
        }

        var (coupons, _) = await _couponRepository.GetListAsync(
            status: input.Status,
            branchId: input.BranchId,
            sorting: input.Sorting,
            maxResultCount: int.MaxValue);

        var rewardLookup = (await _rewardRepository.GetListAsync(r => coupons.Select(c => c.RewardId).Distinct().Contains(r.Id)))
            .ToDictionary(r => r.Id, r => r.NameEn);

        var employeeIds = coupons.Where(c => c.RedeemedByEmployeeId.HasValue).Select(c => c.RedeemedByEmployeeId!.Value).Distinct().ToList();
        var employeeLookup = new Dictionary<Guid, string>();
        foreach (var employeeId in employeeIds)
        {
            var employee = await _identityUserRepository.FindAsync(employeeId);
            if (employee != null)
            {
                employeeLookup[employeeId] = employee.Email;
            }
        }

        var branchIds = coupons.Where(c => c.RedeemedBranchId.HasValue).Select(c => c.RedeemedBranchId!.Value).Distinct().ToList();
        var branches = await _branchRepository.GetListAsync(b => branchIds.Contains(b.Id));
        var branchLookup = branches.ToDictionary(b => b.Id, b => b.Name);

        var memoryStream = new MemoryStream();
        await memoryStream.SaveAsAsync(coupons.Select(c => new CouponExcelDto
        {
            Code = c.Code,
            RewardNameEn = rewardLookup.GetValueOrDefault(c.RewardId) ?? string.Empty,
            Status = c.Status,
            IssuedAt = c.IssuedAt,
            RedeemedAt = c.RedeemedAt,
            RedeemedByEmployeeEmail = c.RedeemedByEmployeeId.HasValue ? employeeLookup.GetValueOrDefault(c.RedeemedByEmployeeId.Value) : null,
            RedeemedBranchName = c.RedeemedBranchId.HasValue ? branchLookup.GetValueOrDefault(c.RedeemedBranchId.Value) : null
        }));
        memoryStream.Seek(0, SeekOrigin.Begin);

        return new RemoteStreamContent(
            memoryStream,
            "Coupons.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
        );
    }

    public async Task<DownloadTokenResultDto> GetDownloadTokenAsync()
    {
        var token = GuidGenerator.Create().ToString("N");

        await _excelDownloadTokenCache.SetAsync(
            token,
            new CouponExcelDownloadTokenCacheItem { Token = token },
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30)
            });

        return new DownloadTokenResultDto
        {
            Token = token
        };
    }

    private async Task SetRewardNamesAsync(List<CouponDto> dtos)
    {
        if (dtos.Count == 0)
        {
            return;
        }

        var rewardIds = dtos.Select(d => d.RewardId).Distinct().ToList();
        var rewards = await _rewardRepository.GetListAsync(r => rewardIds.Contains(r.Id));
        var lookup = rewards.ToDictionary(r => r.Id, r => (r.NameAr, r.NameEn));

        foreach (var dto in dtos)
        {
            if (lookup.TryGetValue(dto.RewardId, out var names))
            {
                dto.RewardNameAr = names.NameAr;
                dto.RewardNameEn = names.NameEn;
            }
        }
    }
}
