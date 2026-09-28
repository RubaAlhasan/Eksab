using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;
using Eksabli.Reports;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Authorization;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Users;
using Eksabli.Memberships;

namespace Eksabli.Wallets;

[RemoteService(IsEnabled = false)]
public class WalletAppService : ApplicationService, IWalletAppService
{
    private readonly IRepository<Membership, Guid> _membershipRepository;
    private readonly IRepository<PointsWallet, Guid> _walletRepository;
    private readonly IRepository<PointsTransaction, Guid> _transactionRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly TransactionListItemBuilder _transactionListItemBuilder;

    public WalletAppService(
        IRepository<Membership, Guid> membershipRepository,
        IRepository<PointsWallet, Guid> walletRepository,
        IRepository<PointsTransaction, Guid> transactionRepository,
        ICurrentTenant currentTenant,
        TransactionListItemBuilder transactionListItemBuilder)
    {
        _membershipRepository = membershipRepository;
        _walletRepository = walletRepository;
        _transactionRepository = transactionRepository;
        _currentTenant = currentTenant;
        _transactionListItemBuilder = transactionListItemBuilder;
    }

    public async Task<PagedResultDto<TransactionListItemDto>> GetMyTransactionHistoryAsync(Guid tenantId, GetMyTransactionHistoryInput input)
    {
        var customerId = CurrentUser.GetId();

        using (_currentTenant.Change(tenantId))
        {
            var membership = await _membershipRepository.FirstOrDefaultAsync(m => m.CustomerId == customerId)
                ?? throw new AbpAuthorizationException("You are not a member of this business.");

            var wallet = await _walletRepository.FirstAsync(w => w.MembershipId == membership.Id);

            var queryable = await _transactionRepository.GetQueryableAsync();
            var filtered = queryable.Where(t => t.WalletId == wallet.Id);
            if (input.Type.HasValue)
            {
                filtered = filtered.Where(t => t.Type == input.Type.Value);
            }

            var query = filtered
                .OrderBy(input.Sorting.IsNullOrWhiteSpace() ? "CreationTime desc" : input.Sorting)
                .Skip(input.SkipCount)
                .Take(input.MaxResultCount);

            var transactions = await AsyncExecuter.ToListAsync(query);
            var totalCount = await AsyncExecuter.CountAsync(filtered);

            // Same grouped-by-process, resolved-names shape the Business/Admin portals show
            // (TransactionListItemBuilder) — minus who-handled-it staff attribution, which is an
            // internal operational detail with no business being shown to the customer it's about.
            var items = await _transactionListItemBuilder.BuildAsync(transactions);
            foreach (var item in items)
            {
                item.StaffId = null;
                item.StaffEmail = null;
            }

            return new PagedResultDto<TransactionListItemDto>(totalCount, items);
        }
    }
}
