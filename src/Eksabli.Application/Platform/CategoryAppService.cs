using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Eksabli.Platform;

public class CategoryAppService : ApplicationService, ICategoryAppService
{
    private readonly ICategoryRepository _repository;

    public CategoryAppService(ICategoryRepository repository)
    {
        _repository = repository;
    }

    public async Task<CategoryDto> GetAsync(Guid id)
    {
        var category = await _repository.GetAsync(id);
        return ObjectMapper.Map<Category, CategoryDto>(category);
    }

    public async Task<PagedResultDto<CategoryDto>> GetListAsync(CategoryListFilterDto input)
    {
        var (categories, totalCount) = await _repository.GetListAsync(
            parentCategoryId: input.ParentCategoryId,
            filterText: input.FilterText,
            sorting: input.Sorting,
            skipCount: input.SkipCount,
            maxResultCount: input.MaxResultCount);

        return new PagedResultDto<CategoryDto>(totalCount, ObjectMapper.Map<List<Category>, List<CategoryDto>>(categories));
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
