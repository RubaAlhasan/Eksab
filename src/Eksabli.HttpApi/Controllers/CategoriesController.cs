using System;
using System.Threading.Tasks;
using Eksabli.Permissions;
using Eksabli.Platform;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Content;

namespace Eksabli.Controllers;

[ApiController]
[Route("api/app/category")]
public class CategoriesController : EksabliController
{
    private readonly ICategoryAppService _categoryAppService;

    public CategoriesController(ICategoryAppService categoryAppService)
    {
        _categoryAppService = categoryAppService;
    }

    // Public taxonomy — read access isn't gated: businesses need it to pick a category at signup
    // (before they have any permission grants) and customers need it to browse discovery by category.
    [AllowAnonymous]
    [HttpGet("{id}")]
    public Task<CategoryDto> GetAsync(Guid id)
    {
        return _categoryAppService.GetAsync(id);
    }

    [AllowAnonymous]
    [HttpGet]
    public Task<PagedResultDto<CategoryDto>> GetListAsync([FromQuery] CategoryListFilterDto input)
    {
        return _categoryAppService.GetListAsync(input);
    }

    [Authorize(EksabliPermissions.Categories.Create)]
    [HttpPost]
    public Task<CategoryDto> CreateAsync(CreateUpdateCategoryDto input)
    {
        return _categoryAppService.CreateAsync(input);
    }

    [Authorize(EksabliPermissions.Categories.Edit)]
    [HttpPut("{id}")]
    public Task<CategoryDto> UpdateAsync(Guid id, CreateUpdateCategoryDto input)
    {
        return _categoryAppService.UpdateAsync(id, input);
    }

    [Authorize(EksabliPermissions.Categories.Delete)]
    [HttpDelete("{id}")]
    public Task DeleteAsync(Guid id)
    {
        return _categoryAppService.DeleteAsync(id);
    }

    // [Consumes] is required here, same as BusinessController.UploadLogoAsync: without it, the controller's
    // default JSON input formatter rejects a multipart upload with 415 before this method is ever reached.
    [Authorize(EksabliPermissions.Categories.Edit)]
    [HttpPut("{id}/icon")]
    [Consumes("multipart/form-data")]
    public Task<CategoryDto> UploadIconAsync(Guid id, [FromForm] IRemoteStreamContent file)
    {
        return _categoryAppService.UploadIconAsync(id, file);
    }

    [Authorize(EksabliPermissions.Categories.Edit)]
    [HttpDelete("{id}/icon")]
    public Task<CategoryDto> RemoveIconAsync(Guid id)
    {
        return _categoryAppService.RemoveIconAsync(id);
    }

    // Public — no [Authorize] — so it works as a plain <img src> URL with no auth context, same as the
    // business logo's own serving endpoint.
    [AllowAnonymous]
    [HttpGet("{id}/icon")]
    public Task<IRemoteStreamContent> GetIconAsync(Guid id)
    {
        return _categoryAppService.GetIconAsync(id);
    }
}
