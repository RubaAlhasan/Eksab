using System;
using Volo.Abp.Application.Dtos;

namespace Eksabli.Platform;

public class CategoryListFilterDto : PagedAndSortedResultRequestDto
{
    public Guid? ParentCategoryId { get; set; }

    public string? FilterText { get; set; }
}
