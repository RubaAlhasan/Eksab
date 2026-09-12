using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Eksabli.BusinessProfiles;
using Eksabli.CustomerProfiles;
using Eksabli.Engagement;
using Eksabli.Wallets;
using Microsoft.Extensions.Caching.Distributed;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement;
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
    private readonly IRepository<CustomerProfile, Guid> _customerProfileRepository;
    private readonly IIdentityUserRepository _identityUserRepository;
    private readonly IRepository<Tenant, Guid> _tenantRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IDataFilter _dataFilter;
    private readonly IDistributedCache _qrCache;

    public MembershipAppService(
        IRepository<Membership, Guid> membershipRepository,
        IRepository<PointsWallet, Guid> walletRepository,
        IRepository<Wallets.Tier, Guid> tierRepository,
        IReferralRepository referralRepository,
        IRepository<BusinessProfile, Guid> businessProfileRepository,
        IRepository<CustomerProfile, Guid> customerProfileRepository,
        IIdentityUserRepository identityUserRepository,
        IRepository<Tenant, Guid> tenantRepository,
        ICurrentTenant currentTenant,
        IDataFilter dataFilter,
        IDistributedCache qrCache)
    {
        _membershipRepository = membershipRepository;
        _walletRepository = walletRepository;
        _tierRepository = tierRepository;
        _referralRepository = referralRepository;
        _businessProfileRepository = businessProfileRepository;
        _customerProfileRepository = customerProfileRepository;
        _identityUserRepository = identityUserRepository;
        _tenantRepository = tenantRepository;
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
            if (businessProfile != null && businessProfile.ApprovalStatus != TenantApprovalStatus.Approved)
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
    //
    // Looks up by Membership.ReferralCode now, not Membership.Id — see that property's own comment.
    // Runs inside JoinAsync's ambient _currentTenant.Change(input.TenantId), so this lookup is already
    // scoped to just this business, matching the code's own per-tenant-unique design.
    private async Task TryCreateReferralAsync(string? referralCode, Membership refereeMembership)
    {
        if (referralCode == null)
        {
            return;
        }

        var referrerMembership = await _membershipRepository.FirstOrDefaultAsync(m => m.ReferralCode == referralCode);
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
            await SetTierProgressAsync(dtos);
            await SetBusinessNamesAsync(dtos);
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

    // Business Portal > Customers "Members" tab. Ambient tenant already scopes Membership/PointsWallet
    // to the caller's own business — no Disable<IMultiTenant>()/tenant switch needed for those, same
    // shape as FollowAppService.GetFollowersAsync. IdentityUser/CustomerProfile ARE Host-realm though
    // (same join PosAppService.LookupCustomerByPhoneAsync already does for a single customer — this
    // is that same join, batched across every member instead of one phone lookup).
    //
    // Loads this tenant's full member set into memory, then filters/sorts/paginates in C# — same
    // "acceptable at this scale" approach AdminTenantAppService already uses for the (much larger,
    // cross-tenant) Businesses list; a single tenant's own member count is smaller by construction.
    // Revisit if a tenant's member count genuinely grows past what's comfortable in memory.
    //
    // Reused by more than just the Customers page (Coupons' name lookup, Notifications' recipient
    // picker, the Subscription page's Active-Members usage count all call this same method) — the
    // MemberFilterDto.HasEarnedPointsAtLeastOnce filter below is opt-in for exactly this reason: only
    // the Customers > Members tab passes it, so this method's default output (every real member,
    // regardless of activity) stays correct for those other callers.
    public async Task<PagedResultDto<MemberDto>> GetMembersAsync(MemberFilterDto input)
    {
        var memberships = await _membershipRepository.GetListAsync();
        var membershipIds = memberships.Select(m => m.Id).ToList();
        var customerIds = memberships.Select(m => m.CustomerId).ToList();

        var wallets = await _walletRepository.GetListAsync(w => membershipIds.Contains(w.MembershipId));
        var walletByMembershipId = wallets.ToDictionary(w => w.MembershipId);

        if (input.HasEarnedPointsAtLeastOnce == true)
        {
            memberships = memberships
                .Where(m => (walletByMembershipId.GetValueOrDefault(m.Id)?.LifetimeEarned ?? 0) > 0)
                .ToList();
        }

        var tierIds = wallets.Where(w => w.CurrentTierId.HasValue).Select(w => w.CurrentTierId!.Value).Distinct().ToList();
        var tierNameById = (await _tierRepository.GetListAsync(t => tierIds.Contains(t.Id)))
            .ToDictionary(t => t.Id, t => t.Name);

        List<IdentityUser> users;
        List<CustomerProfile> profiles;
        using (_currentTenant.Change(null))
        {
            // IIdentityUserRepository is a curated interface (no generic predicate/queryable access,
            // unlike a plain IRepository<T>) — GetListByIdsAsync is its own purpose-built batch lookup.
            users = await _identityUserRepository.GetListByIdsAsync(customerIds);
            profiles = await _customerProfileRepository.GetListAsync(p => customerIds.Contains(p.UserId));
        }
        var userById = users.ToDictionary(u => u.Id);
        var profileByUserId = profiles.ToDictionary(p => p.UserId);

        var dtos = memberships.Select(m =>
        {
            var wallet = walletByMembershipId.GetValueOrDefault(m.Id);
            var profile = profileByUserId.GetValueOrDefault(m.CustomerId);
            var user = userById.GetValueOrDefault(m.CustomerId);
            return new MemberDto
            {
                Id = m.Id,
                CustomerId = m.CustomerId,
                FirstName = profile?.FirstName,
                LastName = profile?.LastName,
                PhoneNumber = user?.PhoneNumber,
                JoinedAt = m.JoinedAt,
                Status = m.Status,
                Balance = wallet?.Balance ?? 0,
                TierId = wallet?.CurrentTierId,
                TierName = wallet?.CurrentTierId.HasValue == true ? tierNameById.GetValueOrDefault(wallet.CurrentTierId.Value) : null,
                LastActiveAt = wallet?.LastModificationTime
            };
        });

        if (!input.FilterText.IsNullOrWhiteSpace())
        {
            var filterText = input.FilterText!;
            dtos = dtos.Where(d =>
                (d.FirstName?.Contains(filterText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (d.LastName?.Contains(filterText, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (d.PhoneNumber?.Contains(filterText, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        if (input.TierId.HasValue)
        {
            dtos = dtos.Where(d => d.TierId == input.TierId.Value);
        }

        if (input.Status.HasValue)
        {
            dtos = dtos.Where(d => d.Status == input.Status.Value);
        }

        var dtoList = dtos.OrderByDescending(d => d.JoinedAt).ToList();
        var totalCount = dtoList.Count;
        var paged = dtoList.Skip(input.SkipCount).Take(input.MaxResultCount).ToList();

        return new PagedResultDto<MemberDto>(totalCount, paged);
    }

    // Single-row counterpart to GetMembersAsync, for the Customer Details page — same real join
    // (Membership -> PointsWallet/Tier -> CustomerProfile/IdentityUser), just for one membership
    // instead of the whole tenant's set. Not refactored to share GetMembersAsync's batched-lookup
    // internals — that method's dictionaries are built for N rows; duplicating the join for a single
    // row here is clearer than threading a "just one" mode through the batch path.
    public async Task<MemberDto> GetMemberAsync(Guid id)
    {
        var membership = await _membershipRepository.GetAsync(id);
        var wallet = await _walletRepository.FirstOrDefaultAsync(w => w.MembershipId == membership.Id);

        string? tierName = null;
        if (wallet?.CurrentTierId != null)
        {
            var tier = await _tierRepository.FindAsync(wallet.CurrentTierId.Value);
            tierName = tier?.Name;
        }

        IdentityUser? user;
        CustomerProfile? profile;
        using (_currentTenant.Change(null)) // IdentityUser/CustomerProfile are Host-realm
        {
            user = await _identityUserRepository.FindAsync(membership.CustomerId);
            // CustomerProfile.Id is its own generated key, NOT the same as UserId (soft reference,
            // no EF FK — same convention as Membership.CustomerId) — FindAsync(customerId) would look
            // up the wrong column entirely; must filter by UserId explicitly.
            profile = await _customerProfileRepository.FirstOrDefaultAsync(p => p.UserId == membership.CustomerId);
        }

        return new MemberDto
        {
            Id = membership.Id,
            CustomerId = membership.CustomerId,
            FirstName = profile?.FirstName,
            LastName = profile?.LastName,
            PhoneNumber = user?.PhoneNumber,
            JoinedAt = membership.JoinedAt,
            Status = membership.Status,
            Balance = wallet?.Balance ?? 0,
            TierId = wallet?.CurrentTierId,
            TierName = tierName,
            LastActiveAt = wallet?.LastModificationTime
        };
    }

    // Called only from within GetMyWalletsAsync's own Disable<IMultiTenant> block — no need to
    // re-disable the filter here.
    //
    // Resolves the tier a wallet's LifetimeEarned qualifies for, rather than reading back
    // PointsWallet.CurrentTierId. That column is a CACHE of this same calculation — TierRecomputeService
    // applies exactly this rule, and only runs when points are awarded. A wallet whose balance arrived
    // any other way (a seed, a migration, a manual correction) carries a stale or null tier, and
    // showing a customer no tier at all when their lifetime points plainly qualify them is worse than
    // showing the one the rule gives. Same relationship Balance has to the transaction ledger.
    //
    // CurrentTierId is still reported as stored, so a caller can tell the two apart.
    private async Task SetTierProgressAsync(List<PointsWalletDto> dtos)
    {
        if (dtos.Count == 0)
        {
            return;
        }

        // One query for every tenant in the result, not one per wallet: a customer with memberships at
        // a dozen businesses was previously a dozen round trips.
        var tenantIds = dtos.Select(d => d.TenantId).Where(id => id.HasValue).Select(id => id!.Value).Distinct().ToList();
        var tiers = await _tierRepository.GetListAsync(t => t.TenantId != null && tenantIds.Contains(t.TenantId!.Value));

        var byTenant = tiers
            .GroupBy(t => t.TenantId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderBy(t => t.MinLifetimePoints).ToList());

        foreach (var dto in dtos)
        {
            if (!dto.TenantId.HasValue || !byTenant.TryGetValue(dto.TenantId.Value, out var ladder))
            {
                continue; // business defines no tiers — the UI hides the whole section
            }

            var current = ladder.LastOrDefault(t => t.MinLifetimePoints <= dto.LifetimeEarned);
            var next = ladder.FirstOrDefault(t => t.MinLifetimePoints > dto.LifetimeEarned);

            dto.CurrentTierName = current?.Name;
            dto.CurrentTierMinLifetimePoints = current?.MinLifetimePoints;
            dto.NextTierName = next?.Name;
            dto.NextTierMinLifetimePoints = next?.MinLifetimePoints;
        }
    }

    // Same "called only from inside GetMyWalletsAsync's own Disable<IMultiTenant> block" shape as
    // SetTierProgressAsync above. Cross-tenant Tenant.Name lookup, safe here specifically because every
    // TenantId being resolved is one this exact customer already has a real wallet in — this is their
    // own cross-business wallet list, not a general-purpose tenant directory (contrast with
    // AdminUserAppService/AdminSubscriptionAppService, which need Disable<IMultiTenant>() precisely
    // because a Host admin is allowed to look at OTHER people's data; here the caller is only ever
    // resolving names for businesses they're personally a member of).
    private async Task SetBusinessNamesAsync(List<PointsWalletDto> dtos)
    {
        var tenantIds = dtos.Where(d => d.TenantId.HasValue).Select(d => d.TenantId!.Value).Distinct().ToList();
        if (tenantIds.Count == 0)
        {
            return;
        }

        var nameByTenantId = (await _tenantRepository.GetListAsync(t => tenantIds.Contains(t.Id)))
            .ToDictionary(t => t.Id, t => t.Name);

        foreach (var dto in dtos)
        {
            if (dto.TenantId.HasValue)
            {
                dto.BusinessName = nameByTenantId.GetValueOrDefault(dto.TenantId.Value);
            }
        }
    }
}
