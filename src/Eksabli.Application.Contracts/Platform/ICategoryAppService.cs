using System;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Content;

namespace Eksabli.Platform;

// Platform-wide, Host-realm — exposed via an explicit controller
// (src/Eksabli.HttpApi/Controllers/CategoriesController.cs).
[RemoteService(IsEnabled = false)]
public interface ICategoryAppService : IApplicationService
{
    Task<CategoryDto> GetAsync(Guid id);

    Task<PagedResultDto<CategoryDto>> GetListAsync(CategoryListFilterDto input);

    Task<CategoryDto> CreateAsync(CreateUpdateCategoryDto input);

    Task<CategoryDto> UpdateAsync(Guid id, CreateUpdateCategoryDto input);

    Task DeleteAsync(Guid id);

    // Same upload/remove/serve shape as BusinessAppService's logo, by category id rather than "the
    // caller's own profile" — an admin manages many categories, there is no single ambient one.
    Task<CategoryDto> UploadIconAsync(Guid id, IRemoteStreamContent file);

    Task<CategoryDto> RemoveIconAsync(Guid id);

    Task<IRemoteStreamContent> GetIconAsync(Guid id);
}
