using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace Eksabli.Platform;

public interface ICategoryRepository : IRepository<Category, Guid>
{
    Task<(List<Category> Items, int TotalCount)> GetListAsync(
        Guid? parentCategoryId = null,
        string? filterText = null,
        string? sorting = null,
        int skipCount = 0,
        int maxResultCount = int.MaxValue,
        CancellationToken cancellationToken = default);
}
