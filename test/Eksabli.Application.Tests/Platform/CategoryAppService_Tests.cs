using System;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Modularity;
using Xunit;

namespace Eksabli.Platform;

public abstract class CategoryAppService_Tests<TStartupModule> : EksabliApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly ICategoryAppService _categoryAppService;

    protected CategoryAppService_Tests()
    {
        _categoryAppService = GetRequiredService<ICategoryAppService>();
    }

    [Fact]
    public async Task Should_Create_List_Update_And_Delete_A_Category()
    {
        var created = await WithUnitOfWorkAsync(() => _categoryAppService.CreateAsync(new CreateUpdateCategoryDto
        {
            NameAr = "مقاهي",
            NameEn = "Cafes"
        }));
        created.ParentCategoryId.ShouldBeNull();

        var list = await WithUnitOfWorkAsync(() => _categoryAppService.GetListAsync(new CategoryListFilterDto()));
        list.Items.ShouldContain(c => c.Id == created.Id);

        var updated = await WithUnitOfWorkAsync(() => _categoryAppService.UpdateAsync(created.Id, new CreateUpdateCategoryDto
        {
            NameAr = "مقاهي ومطاعم",
            NameEn = "Cafes & Restaurants"
        }));
        updated.NameEn.ShouldBe("Cafes & Restaurants");

        await WithUnitOfWorkAsync(() => _categoryAppService.DeleteAsync(created.Id));
        var afterDelete = await WithUnitOfWorkAsync(() => _categoryAppService.GetListAsync(new CategoryListFilterDto()));
        afterDelete.Items.ShouldNotContain(c => c.Id == created.Id);
    }

    [Fact]
    public async Task Should_Create_A_Subcategory_Under_A_Parent()
    {
        var parent = await WithUnitOfWorkAsync(() => _categoryAppService.CreateAsync(new CreateUpdateCategoryDto
        {
            NameAr = "مطاعم",
            NameEn = "Restaurants"
        }));

        var child = await WithUnitOfWorkAsync(() => _categoryAppService.CreateAsync(new CreateUpdateCategoryDto
        {
            NameAr = "وجبات سريعة",
            NameEn = "Fast Food",
            ParentCategoryId = parent.Id
        }));

        var children = await WithUnitOfWorkAsync(() => _categoryAppService.GetListAsync(new CategoryListFilterDto { ParentCategoryId = parent.Id }));
        children.Items.ShouldContain(c => c.Id == child.Id);
    }

    [Fact]
    public async Task Should_Not_Allow_A_Category_To_Be_Its_Own_Parent()
    {
        var category = await WithUnitOfWorkAsync(() => _categoryAppService.CreateAsync(new CreateUpdateCategoryDto
        {
            NameAr = "متاجر",
            NameEn = "Shops"
        }));

        await Should.ThrowAsync<UserFriendlyException>(() => WithUnitOfWorkAsync(() =>
            _categoryAppService.UpdateAsync(category.Id, new CreateUpdateCategoryDto
            {
                NameAr = "متاجر",
                NameEn = "Shops",
                ParentCategoryId = category.Id
            })));
    }
}
