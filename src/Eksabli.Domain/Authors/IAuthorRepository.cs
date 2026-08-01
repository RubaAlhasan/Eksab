using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace Eksabli.Authors;

public interface IAuthorRepository : IRepository<Author, Guid>
{
    Task<(List<Author> Items, int TotalCount)> GetListAsync(
        string? filterText = null,
        string? sorting = null,
        int skipCount = 0,
        int maxResultCount = int.MaxValue,
        CancellationToken cancellationToken = default);
}
