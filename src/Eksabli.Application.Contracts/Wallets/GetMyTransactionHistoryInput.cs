using Volo.Abp.Application.Dtos;

namespace Eksabli.Wallets;

public class GetMyTransactionHistoryInput : PagedAndSortedResultRequestDto
{
    public PointsTransactionType? Type { get; set; }
}
