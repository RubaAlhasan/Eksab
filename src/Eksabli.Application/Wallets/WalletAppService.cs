using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;
using Eksabli.Memberships;

namespace Eksabli.Wallets;

public class WalletAppService : ApplicationService, IWalletAppService
{
    private readonly IRepository<Membership, Guid> _membershipRepository;
    private readonly IRepository<PointsWallet, Guid> _walletRepository;
    private readonly IRepository<PointsTransaction, Guid> _transactionRepository;
    private readonly ICurrentTenant _currentTenant;

    public WalletAppService(
        IRepository<Membership, Guid> membershipRepository,
        IRepository<PointsWallet, Guid> walletRepository,
        IRepository<PointsTransaction, Guid> transactionRepository,
        ICurrentTenant currentTenant)
    {
        _membershipRepository = membershipRepository;
        _walletRepository = walletRepository;
        _transactionRepository = transactionRepository;
        _currentTenant = currentTenant;
    }

    public async Task<PagedResultDto<PointsTransactionDto>> GetMyTransactionHistoryAsync(Guid tenantId, PagedAndSortedResultRequestDto input)
    {
        var customerId = CurrentUser.GetId();

        using (_currentTenant.Change(tenantId))
        {
            var membership = await _membershipRepository.FirstOrDefaultAsync(m => m.CustomerId == customerId)
                ?? throw new AbpAuthorizationException("You are not a member of this business.");

            var wallet = await _walletRepository.FirstAsync(w => w.MembershipId == membership.Id);

            var queryable = await _transactionRepository.GetQueryableAsync();
            var query = queryable
                .Where(t => t.WalletId == wallet.Id)
                .OrderBy(input.Sorting.IsNullOrWhiteSpace() ? "CreationTime desc" : input.Sorting)
                .Skip(input.SkipCount)
                .Take(input.MaxResultCount);

            var transactions = await AsyncExecuter.ToListAsync(query);
            var totalCount = await AsyncExecuter.CountAsync(queryable.Where(t => t.WalletId == wallet.Id));

            var dtos = ObjectMapper.Map<List<PointsTransaction>, List<PointsTransactionDto>>(transactions);
            return new PagedResultDto<PointsTransactionDto>(totalCount, dtos);
        }
    }
}
