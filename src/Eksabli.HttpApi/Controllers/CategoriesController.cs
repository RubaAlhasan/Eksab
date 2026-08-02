using System;
using System.Threading.Tasks;
using Eksabli.Permissions;
using Eksabli.Platform;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;

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
}
