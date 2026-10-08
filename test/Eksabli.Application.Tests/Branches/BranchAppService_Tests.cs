using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Modularity;
using Volo.Abp.Validation;
using Xunit;

namespace Eksabli.Branches;

public abstract class BranchAppService_Tests<TStartupModule> : EksabliApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly IBranchAppService _branchAppService;

    protected BranchAppService_Tests()
    {
        _branchAppService = GetRequiredService<IBranchAppService>();
    }

    [Fact]
    public async Task Should_Create_And_List_A_Branch()
    {
        var created = await WithUnitOfWorkAsync(() => _branchAppService.CreateAsync(new CreateUpdateBranchDto
        {
            Name = "Downtown Branch",
            Address = "1 Main St"
        }));

        created.Id.ShouldNotBe(System.Guid.Empty);
        created.Name.ShouldBe("Downtown Branch");

        var list = await WithUnitOfWorkAsync(() => _branchAppService.GetListAsync(new PagedAndSortedResultRequestDto()));
        list.Items.ShouldContain(b => b.Id == created.Id);
    }

    [Fact]
    public async Task Should_Not_Create_A_Branch_Without_Name()
    {
        var exception = await Assert.ThrowsAsync<AbpValidationException>(async () =>
        {
            await WithUnitOfWorkAsync(() => _branchAppService.CreateAsync(new CreateUpdateBranchDto { Name = "" }));
        });

        exception.ValidationErrors.ShouldContain(err => err.MemberNames.Any(mem => mem == "Name"));
    }

    [Fact]
    public async Task Should_Reject_Creating_A_Branch_Beyond_The_Plan_Limit()
    {
        // No subscription/feature override in this test context — falls back to the
        // EksabliFeatureDefinitionProvider default of "1" (Starter-tier value).
        await WithUnitOfWorkAsync(() => _branchAppService.CreateAsync(new CreateUpdateBranchDto { Name = "First Branch" }));

        await Assert.ThrowsAsync<Volo.Abp.UserFriendlyException>(async () =>
        {
            await WithUnitOfWorkAsync(() => _branchAppService.CreateAsync(new CreateUpdateBranchDto { Name = "Second Branch" }));
        });
    }

    [Fact]
    public async Task Should_Save_And_Return_Structured_Opening_Hours()
    {
        var created = await WithUnitOfWorkAsync(() => _branchAppService.CreateAsync(new CreateUpdateBranchDto
        {
            Name = "Downtown Branch",
            OpeningHours = new List<DayOpeningHoursDto>
            {
                new() { DayOfWeek = DayOfWeek.Monday, IsClosed = false, OpenTime = "09:00", CloseTime = "22:00" },
                new() { DayOfWeek = DayOfWeek.Sunday, IsClosed = true },
            },
        }));

        created.OpeningHours.Count.ShouldBe(2);
        var monday = created.OpeningHours.Single(d => d.DayOfWeek == DayOfWeek.Monday);
        monday.IsClosed.ShouldBeFalse();
        monday.OpenTime.ShouldBe("09:00");
        monday.CloseTime.ShouldBe("22:00");
        created.OpeningHours.Single(d => d.DayOfWeek == DayOfWeek.Sunday).IsClosed.ShouldBeTrue();

        var fetched = await WithUnitOfWorkAsync(() => _branchAppService.GetAsync(created.Id));
        fetched.OpeningHours.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Should_Reject_Opening_Hours_With_An_Invalid_Time()
    {
        await Assert.ThrowsAsync<UserFriendlyException>(async () =>
        {
            await WithUnitOfWorkAsync(() => _branchAppService.CreateAsync(new CreateUpdateBranchDto
            {
                Name = "Downtown Branch",
                OpeningHours = new List<DayOpeningHoursDto>
                {
                    new() { DayOfWeek = DayOfWeek.Monday, IsClosed = false, OpenTime = "25:99", CloseTime = "22:00" },
                },
            }));
        });
    }

    [Fact]
    public async Task Should_Reject_Opening_Hours_That_List_The_Same_Day_Twice()
    {
        await Assert.ThrowsAsync<UserFriendlyException>(async () =>
        {
            await WithUnitOfWorkAsync(() => _branchAppService.CreateAsync(new CreateUpdateBranchDto
            {
                Name = "Downtown Branch",
                OpeningHours = new List<DayOpeningHoursDto>
                {
                    new() { DayOfWeek = DayOfWeek.Monday, IsClosed = false, OpenTime = "09:00", CloseTime = "17:00" },
                    new() { DayOfWeek = DayOfWeek.Monday, IsClosed = false, OpenTime = "18:00", CloseTime = "22:00" },
                },
            }));
        });
    }
}
