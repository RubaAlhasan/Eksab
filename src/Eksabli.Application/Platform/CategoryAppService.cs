using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Eksabli.BusinessProfiles;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.BlobStoring;
using Volo.Abp.Content;
using Volo.Abp.Data;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.MultiTenancy;

namespace Eksabli.Platform;

[RemoteService(IsEnabled = false)]
public class CategoryAppService : ApplicationService, ICategoryAppService
{
    private readonly ICategoryRepository _repository;
    private readonly IRepository<BusinessProfile, Guid> _businessProfileRepository;
    private readonly IBlobContainer<CategoryIconContainer> _iconContainer;
    private readonly IDataFilter _dataFilter;

    public CategoryAppService(
        ICategoryRepository repository,
        IRepository<BusinessProfile, Guid> businessProfileRepository,
        IBlobContainer<CategoryIconContainer> iconContainer,
        IDataFilter dataFilter)
    {
        _repository = repository;
        _businessProfileRepository = businessProfileRepository;
        _iconContainer = iconContainer;
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
        await _repository.InsertAsync(category);
        return ObjectMapper.Map<Category, CategoryDto>(category);
    }

    public async Task<CategoryDto> UpdateAsync(Guid id, CreateUpdateCategoryDto input)
    {
        var category = await _repository.GetAsync(id);
        category.SetNames(input.NameAr, input.NameEn);
        category.SetParent(input.ParentCategoryId);
        await _repository.UpdateAsync(category);
        return ObjectMapper.Map<Category, CategoryDto>(category);
    }

    public async Task DeleteAsync(Guid id)
    {
        var category = await _repository.GetAsync(id);
        await _repository.DeleteAsync(category);

        if (!category.IconBlobName.IsNullOrWhiteSpace())
        {
            // Best-effort, same reasoning as replacing a logo: the category is already gone, so a failed
            // blob delete here only costs storage, not correctness.
            await _iconContainer.DeleteAsync(category.IconBlobName!);
        }
    }

    // Same validate-read-save-swap shape as BusinessAppService.UploadLogoAsync, keyed by this category's id
    // rather than "the caller's own profile".
    public async Task<CategoryDto> UploadIconAsync(Guid id, IRemoteStreamContent file)
    {
        var contentType = file.ContentType?.ToLowerInvariant();
        if (contentType.IsNullOrWhiteSpace() || Array.IndexOf(CategoryConsts.AllowedIconContentTypes, contentType) < 0)
        {
            throw new UserFriendlyException("Only PNG, JPEG, or WebP images are allowed for a category icon.");
        }

        var category = await _repository.GetAsync(id);

        using var content = await ReadBoundedAsync(file.GetStream(), CategoryConsts.MaxIconFileSizeBytes);

        var blobName = $"{GuidGenerator.Create():N}";
        await _iconContainer.SaveAsync(blobName, content);

        var oldBlobName = category.IconBlobName;
        category.SetIcon(blobName, contentType);
        await _repository.UpdateAsync(category);

        if (!oldBlobName.IsNullOrWhiteSpace())
        {
            // Best-effort — an orphaned old blob costs storage, not correctness, and the new icon is
            // already saved and already the one the entity references, so a delete failure here
            // shouldn't fail the whole upload.
            await _iconContainer.DeleteAsync(oldBlobName!);
        }

        return ObjectMapper.Map<Category, CategoryDto>(category);
    }

    public async Task<CategoryDto> RemoveIconAsync(Guid id)
    {
        var category = await _repository.GetAsync(id);

        if (!category.IconBlobName.IsNullOrWhiteSpace())
        {
            await _iconContainer.DeleteAsync(category.IconBlobName!);
        }

        category.SetIcon(null, null);
        await _repository.UpdateAsync(category);
        return ObjectMapper.Map<Category, CategoryDto>(category);
    }

    // Public taxonomy, so this is reachable anonymously (see the controller) — no tenant context to disable
    // a filter on either, since Category itself is not IMultiTenant.
    public async Task<IRemoteStreamContent> GetIconAsync(Guid id)
    {
        var category = await _repository.GetAsync(id);
        if (category.IconBlobName.IsNullOrWhiteSpace())
        {
            throw new EntityNotFoundException(typeof(Category), id);
        }

        var stream = await _iconContainer.GetAsync(category.IconBlobName!);
        return new RemoteStreamContent(stream, "icon", category.IconContentType ?? "application/octet-stream");
    }

    private static async Task<MemoryStream> ReadBoundedAsync(Stream source, int maxBytes)
    {
        var buffer = new byte[81920];
        var destination = new MemoryStream();
        int read;
        while ((read = await source.ReadAsync(buffer, 0, buffer.Length)) > 0)
        {
            if (destination.Length + read > maxBytes)
            {
                throw new UserFriendlyException($"The icon file is too large. Maximum size is {maxBytes / 1024} KB.");
            }
            await destination.WriteAsync(buffer, 0, read);
        }
        destination.Seek(0, SeekOrigin.Begin);
        return destination;
    }
}
