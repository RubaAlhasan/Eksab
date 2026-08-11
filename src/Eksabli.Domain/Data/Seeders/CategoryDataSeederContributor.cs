using System;
using System.Threading.Tasks;
using Eksabli.Platform;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace Eksabli.Data.Seeders;

// Platform-wide business category taxonomy — see Eksabli.Platform.Category. Seeded once so tenant
// onboarding (BusinessProfile.CategoryId) and the admin panel category management screen always
// have a baseline list to work with. Discoverable by ABP's IDataSeeder (DbMigrator flow) and also
// invoked explicitly by SeedService (HttpApi.Host startup flow) — see SeedService.cs.
public class CategoryDataSeederContributor : IDataSeedContributor, ITransientDependency
{
    private readonly IRepository<Category, Guid> _categoryRepository;

    public CategoryDataSeederContributor(IRepository<Category, Guid> categoryRepository)
    {
        _categoryRepository = categoryRepository;
    }

    // Convenience overload for callers (SeedService) that have no tenant/context to pass —
    // Category is platform-wide, never tenant-scoped, so seeding never needs one.
    public Task SeedAsync() => SeedAsync(new DataSeedContext());

    public async Task SeedAsync(DataSeedContext context)
    {
        if (await _categoryRepository.GetCountAsync() > 0)
        {
            return;
        }

        await CreateCategoryAsync("مطاعم ومقاهي", "Restaurants & Cafes");
        await CreateCategoryAsync("تجزئة وتسوق", "Retail & Shopping");
        await CreateCategoryAsync("جمال وعناية", "Beauty & Wellness");
        await CreateCategoryAsync("صحة ولياقة", "Health & Fitness");
        await CreateCategoryAsync("ترفيه", "Entertainment");
        await CreateCategoryAsync("خدمات", "Services");
        await CreateCategoryAsync("تعليم", "Education");
        await CreateCategoryAsync("سفر وضيافة", "Travel & Hospitality");
    }

    private async Task CreateCategoryAsync(string nameAr, string nameEn)
    {
        var category = Category.Create(Guid.NewGuid(), nameAr, nameEn);
        await _categoryRepository.InsertAsync(category, autoSave: true);
    }
}
