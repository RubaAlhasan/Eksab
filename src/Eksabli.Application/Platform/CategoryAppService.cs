using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Eksabli.BusinessProfiles;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace Eksabli.Platform;

[RemoteService(IsEnabled = false)]
public class CategoryAppService : ApplicationService, ICategoryAppService
{
    private readonly ICategoryRepository _repository;
    private readonly IRepository<BusinessProfile, Guid> _businessProfileRepository;
    private readonly IDataFilter _dataFilter;

    public CategoryAppService(
        ICategoryRepository repository,
        IRepository<BusinessProfile, Guid> businessProfileRepository,
        IDataFilter dataFilter)
    {
        _repository = repository;
        _businessProfileRepository = businessProfileRepository;
        _dataFilter = dataFilter;
    }

    public async Task<CategoryDto> GetAsync(Guid id)
    {
        var category = await _repository.GetAsync(id);
        var dto = ObjectMapper.Map<Category, CategoryDto>(category);
        dto.BusinessCount = await CountBusinessesAsync(category.Id);
        return dto;
    }

    public async Task<PagedResultDto<CategoryDto>> GetListAsync(CategoryListFilterDto input)
    {
        var (categories, totalCount) = await _repository.GetListAsync(
            parentCategoryId: input.ParentCategoryId,
            filterText: input.FilterText,
            sorting: input.Sorting,
            skipCount: input.SkipCount,
            maxResultCount: input.MaxResultCount);

        var dtos = ObjectMapper.Map<List<Category>, List<CategoryDto>>(categories);
        var counts = await CountBusinessesByCategoryAsync(categories.Select(c => c.Id));
        foreach (var dto in dtos)
        {
            dto.BusinessCount = counts.GetValueOrDefault(dto.Id);
        }

        return new PagedResultDto<CategoryDto>(totalCount, dtos);
    }

    // BusinessProfile is IMultiTenant (one per tenant) — Categories are platform-wide, so counting how
    // many businesses (across every tenant) use a category requires disabling the tenant filter, same
    // as AdminTenantAppService does for its own platform-wide reads. Loads id+categoryId for every
    // BusinessProfile rather than querying per-category, matching this codebase's existing "bounded
    // in-memory batch" scale assumption (see AdminTenantAppService/AdminSubscriptionsComponent
    // comments) rather than N+1 round-trips.
    private async Task<Dictionary<Guid, int>> CountBusinessesByCategoryAsync(IEnumerable<Guid> categoryIds)
    {
        var categoryIdSet = categoryIds.ToHashSet();
        using (_dataFilter.Disable<IMultiTenant>())
        {
            var profiles = await _businessProfileRepository.GetListAsync(p =>
                p.CategoryId.HasValue && categoryIdSet.Contains(p.CategoryId.Value));

            return profiles
                .GroupBy(p => p.CategoryId!.Value)
                .ToDictionary(g => g.Key, g => g.Count());
        }
    }

    private async Task<int> CountBusinessesAsync(Guid categoryId)
    {
        using (_dataFilter.Disable<IMultiTenant>())
        {
            return await _businessProfileRepository.CountAsync(p => p.CategoryId == categoryId);
        }
    }

    public async Task<CategoryDto> CreateAsync(CreateUpdateCategoryDto input)
    {
        var category = Category.Create(GuidGenerator.Create(), input.NameAr, input.NameEn, input.ParentCategoryId);
        category.SetIconBlobName(input.IconBlobName);
        await _repository.InsertAsync(category);
        return ObjectMapper.Map<Category, CategoryDto>(category);
    }

    public async Task<CategoryDto> UpdateAsync(Guid id, CreateUpdateCategoryDto input)
    {
        var category = await _repository.GetAsync(id);
        category.SetNames(input.NameAr, input.NameEn);
        category.SetParent(input.ParentCategoryId);
        category.SetIconBlobName(input.IconBlobName);
        await _repository.UpdateAsync(category);
        return ObjectMapper.Map<Category, CategoryDto>(category);
    }

    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }
}
