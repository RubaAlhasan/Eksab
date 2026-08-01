using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Eksabli.Wallets;
using Microsoft.Extensions.Caching.Distributed;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;

namespace Eksabli.Memberships;

public class MembershipAppService : ApplicationService, IMembershipAppService
{
    private readonly IRepository<Membership, Guid> _membershipRepository;
    private readonly IRepository<PointsWallet, Guid> _walletRepository;
    private readonly IRepository<Wallets.Tier, Guid> _tierRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IDataFilter _dataFilter;
    private readonly IDistributedCache _qrCache;

    public MembershipAppService(
        IRepository<Membership, Guid> membershipRepository,
        IRepository<PointsWallet, Guid> walletRepository,
        IRepository<Wallets.Tier, Guid> tierRepository,
        ICurrentTenant currentTenant,
        IDataFilter dataFilter,
        IDistributedCache qrCache)
    {
        _membershipRepository = membershipRepository;
        _walletRepository = walletRepository;
        _tierRepository = tierRepository;
        _currentTenant = currentTenant;
        _dataFilter = dataFilter;
        _qrCache = qrCache;
    }

    public async Task<MembershipDto> JoinAsync(JoinBusinessDto input)
    {
        var customerId = CurrentUser.GetId();

        using (_currentTenant.Change(input.TenantId))
        {
            var existing = await _membershipRepository.FirstOrDefaultAsync(m => m.CustomerId == customerId);
            if (existing != null)
            {
                throw new UserFriendlyException("You are already a member of this business.");
            }

            var membership = Membership.Create(GuidGenerator.Create(), customerId, Clock.Now);
            await _membershipRepository.InsertAsync(membership, autoSave: true);

            var wallet = PointsWallet.Create(GuidGenerator.Create(), membership.Id);
            await _walletRepository.InsertAsync(wallet, autoSave: true);

            return ObjectMapper.Map<Membership, MembershipDto>(membership);
        }
    }

    public async Task<List<MembershipDto>> GetMyMembershipsAsync()
    {
        var customerId = CurrentUser.GetId();

        using (_dataFilter.Disable<IMultiTenant>())
        {
            var memberships = await _membershipRepository.GetListAsync(m => m.CustomerId == customerId);
            return ObjectMapper.Map<List<Membership>, List<MembershipDto>>(memberships);
        }
    }

    public async Task<List<PointsWalletDto>> GetMyWalletsAsync()
    {
        var customerId = CurrentUser.GetId();

        using (_dataFilter.Disable<IMultiTenant>())
        {
            var membershipIds = (await _membershipRepository.GetListAsync(m => m.CustomerId == customerId))
                .Select(m => m.Id)
                .ToList();

            var wallets = await _walletRepository.GetListAsync(w => membershipIds.Contains(w.MembershipId));
            var dtos = ObjectMapper.Map<List<PointsWallet>, List<PointsWalletDto>>(wallets);
            await SetTierNamesAsync(dtos);
            return dtos;
        }
    }

    public async Task<WalletQrTokenResultDto> GetMyWalletQrTokenAsync()
    {
        const int expiresInSeconds = 90;
        var token = GuidGenerator.Create().ToString("N");

        var item = new WalletQrCacheItem { CustomerId = CurrentUser.GetId() };
        await _qrCache.SetAsync(
            WalletQrCacheItem.CacheKeyPrefix + token,
            JsonSerializer.SerializeToUtf8Bytes(item),
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(expiresInSeconds) });

        return new WalletQrTokenResultDto { Token = token, ExpiresInSeconds = expiresInSeconds };
    }

    // Called only from within GetMyWalletsAsync's own Disable<IMultiTenant> block — no need to
    // re-disable the filter here.
    private async Task SetTierNamesAsync(List<PointsWalletDto> dtos)
    {
        foreach (var dto in dtos)
        {
            if (dto.CurrentTierId.HasValue)
            {
                var tier = await _tierRepository.FindAsync(dto.CurrentTierId.Value);
                dto.CurrentTierName = tier?.Name;
            }
        }
    }
}
