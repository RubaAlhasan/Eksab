using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Eksabli.BusinessProfiles;
using Eksabli.Engagement;
using Eksabli.Wallets;
using Microsoft.Extensions.Caching.Distributed;
using Volo.Abp;
using Volo.Abp.Application.Services;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;

namespace Eksabli.Memberships;

[RemoteService(IsEnabled = false)]
public class MembershipAppService : ApplicationService, IMembershipAppService
{
    private readonly IRepository<Membership, Guid> _membershipRepository;
    private readonly IRepository<PointsWallet, Guid> _walletRepository;
    private readonly IRepository<Wallets.Tier, Guid> _tierRepository;
    private readonly IReferralRepository _referralRepository;
    private readonly IRepository<BusinessProfile, Guid> _businessProfileRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IDataFilter _dataFilter;
    private readonly IDistributedCache _qrCache;

    public MembershipAppService(
        IRepository<Membership, Guid> membershipRepository,
        IRepository<PointsWallet, Guid> walletRepository,
        IRepository<Wallets.Tier, Guid> tierRepository,
        IReferralRepository referralRepository,
        IRepository<BusinessProfile, Guid> businessProfileRepository,
        ICurrentTenant currentTenant,
        IDataFilter dataFilter,
        IDistributedCache qrCache)
    {
        _membershipRepository = membershipRepository;
        _walletRepository = walletRepository;
        _tierRepository = tierRepository;
        _referralRepository = referralRepository;
        _businessProfileRepository = businessProfileRepository;
        _currentTenant = currentTenant;
        _dataFilter = dataFilter;
        _qrCache = qrCache;
    }

    public async Task<MembershipDto> JoinAsync(JoinBusinessDto input)
    {
        var customerId = CurrentUser.GetId();

        using (_currentTenant.Change(input.TenantId))
        {
            // FirstOrDefault, not Single — a missing BusinessProfile (shouldn't happen outside tests/
            // seed data that bypassed BusinessAppService.RegisterAsync) fails open rather than blocking
            // every join with an unrelated 500.
            var businessProfile = await _businessProfileRepository.FirstOrDefaultAsync();
            if (businessProfile?.ApprovalStatus == TenantApprovalStatus.Suspended)
            {
                throw new UserFriendlyException("This business isn't currently accepting new members.");
            }

            var existing = await _membershipRepository.FirstOrDefaultAsync(m => m.CustomerId == customerId);
            if (existing != null)
            {
                throw new UserFriendlyException("You are already a member of this business.");
            }

            var membership = Membership.Create(GuidGenerator.Create(), customerId, Clock.Now);
            await _membershipRepository.InsertAsync(membership, autoSave: true);

            var wallet = PointsWallet.Create(GuidGenerator.Create(), membership.Id);
            await _walletRepository.InsertAsync(wallet, autoSave: true);

            await TryCreateReferralAsync(input.ReferralCode, membership);

            return ObjectMapper.Map<Membership, MembershipDto>(membership);
        }
    }

    // Invalid, self-referral, or already-referred codes are ignored rather than rejected — a bad
    // referral code shouldn't block the join itself. Actual bonus payout happens later, on the
    // referee's first purchase (Engagement.ReferralCompletionService via PosAppService).
    private async Task TryCreateReferralAsync(Guid? referralCode, Membership refereeMembership)
    {
        if (!referralCode.HasValue)
        {
            return;
        }

        var referrerMembership = await _membershipRepository.FindAsync(referralCode.Value);
        if (referrerMembership == null || referrerMembership.CustomerId == refereeMembership.CustomerId)
        {
            return;
        }

        var referral = Referral.Create(GuidGenerator.Create(), referrerMembership.Id, refereeMembership.CustomerId);
        await _referralRepository.InsertAsync(referral);
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
