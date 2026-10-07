using System;
using System.IO;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Volo.Abp.BlobStoring;
using Volo.Abp.Content;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Modularity;
using Xunit;

namespace Eksabli.Platform;

public abstract class CategoryAppService_Tests<TStartupModule> : EksabliApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly ICategoryAppService _categoryAppService;
    private readonly IBlobContainer<CategoryIconContainer> _iconContainer;

    protected CategoryAppService_Tests()
    {
        _categoryAppService = GetRequiredService<ICategoryAppService>();
        _iconContainer = GetRequiredService<IBlobContainer<CategoryIconContainer>>();
    }

    private static RemoteStreamContent IconFile(byte[] bytes, string contentType = "image/png") =>
        new RemoteStreamContent(new MemoryStream(bytes), "icon.png", contentType);

    private Task<CategoryDto> CreateCategoryAsync(string nameEn) => WithUnitOfWorkAsync(() => _categoryAppService.CreateAsync(new CreateUpdateCategoryDto
    {
        NameAr = nameEn,
        NameEn = nameEn,
    }));

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

    [Fact]
    public async Task Should_Upload_And_Serve_A_Category_Icon()
    {
        var category = await CreateCategoryAsync("Bakeries");

        var uploaded = await WithUnitOfWorkAsync(() => _categoryAppService.UploadIconAsync(category.Id, IconFile(new byte[] { 1, 2, 3 })));
        uploaded.IconBlobName.ShouldNotBeNullOrWhiteSpace();

        var served = await WithUnitOfWorkAsync(() => _categoryAppService.GetIconAsync(category.Id));
        served.ContentType.ShouldBe("image/png");
        using var stream = new MemoryStream();
        await served.GetStream().CopyToAsync(stream);
        stream.ToArray().ShouldBe(new byte[] { 1, 2, 3 });
    }

    [Fact]
    public async Task Should_Delete_The_Old_Blob_When_Replacing_A_Category_Icon()
    {
        var category = await CreateCategoryAsync("Pharmacies");

        var first = await WithUnitOfWorkAsync(() => _categoryAppService.UploadIconAsync(category.Id, IconFile(new byte[] { 1, 2, 3 })));
        var firstBlobName = first.IconBlobName!;

        var second = await WithUnitOfWorkAsync(() => _categoryAppService.UploadIconAsync(category.Id, IconFile(new byte[] { 4, 5, 6 })));
        second.IconBlobName.ShouldNotBe(firstBlobName);

        (await WithUnitOfWorkAsync(() => _iconContainer.ExistsAsync(firstBlobName))).ShouldBeFalse();
    }

    [Fact]
    public async Task Should_Remove_A_Category_Icon()
    {
        var category = await CreateCategoryAsync("Gyms");
        var uploaded = await WithUnitOfWorkAsync(() => _categoryAppService.UploadIconAsync(category.Id, IconFile(new byte[] { 1, 2, 3 })));

        var removed = await WithUnitOfWorkAsync(() => _categoryAppService.RemoveIconAsync(category.Id));
        removed.IconBlobName.ShouldBeNullOrWhiteSpace();

        (await WithUnitOfWorkAsync(() => _iconContainer.ExistsAsync(uploaded.IconBlobName!))).ShouldBeFalse();
        await Should.ThrowAsync<EntityNotFoundException>(() => WithUnitOfWorkAsync(() => _categoryAppService.GetIconAsync(category.Id)));
    }

    [Fact]
    public async Task Should_Not_Allow_An_Unsupported_Content_Type_For_A_Category_Icon()
    {
        var category = await CreateCategoryAsync("Spas");

        await Should.ThrowAsync<UserFriendlyException>(() => WithUnitOfWorkAsync(() =>
            _categoryAppService.UploadIconAsync(category.Id, IconFile(new byte[] { 1, 2, 3 }, "application/pdf"))));
    }

    [Fact]
    public async Task Should_Throw_When_Getting_The_Icon_Of_A_Category_Without_One()
    {
        var category = await CreateCategoryAsync("Salons");

        await Should.ThrowAsync<EntityNotFoundException>(() => WithUnitOfWorkAsync(() => _categoryAppService.GetIconAsync(category.Id)));
    }

    [Fact]
    public async Task Should_Delete_The_Icon_Blob_When_The_Category_Is_Deleted()
    {
        var category = await CreateCategoryAsync("Clinics");
        var uploaded = await WithUnitOfWorkAsync(() => _categoryAppService.UploadIconAsync(category.Id, IconFile(new byte[] { 1, 2, 3 })));

        await WithUnitOfWorkAsync(() => _categoryAppService.DeleteAsync(category.Id));

        (await WithUnitOfWorkAsync(() => _iconContainer.ExistsAsync(uploaded.IconBlobName!))).ShouldBeFalse();
    }
}
