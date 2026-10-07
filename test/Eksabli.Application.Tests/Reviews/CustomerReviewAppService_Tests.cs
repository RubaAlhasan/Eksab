using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Eksabli.Memberships;
using Shouldly;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Modularity;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Security.Claims;
using Volo.Abp.TenantManagement;
using Volo.Abp.Validation;
using Xunit;

namespace Eksabli.Reviews;

public abstract class CustomerReviewAppService_Tests<TStartupModule> : EksabliApplicationTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{
    private readonly ICustomerReviewAppService _reviewService;
    private readonly TenantManager _tenantManager;
    private readonly ITenantRepository _tenantRepository;
    private readonly IRepository<Membership, Guid> _membershipRepository;
    private readonly IRepository<Eksabli.CustomerProfiles.CustomerProfile, Guid> _customerProfileRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly ICurrentPrincipalAccessor _currentPrincipalAccessor;

    protected CustomerReviewAppService_Tests()
    {
        _reviewService = GetRequiredService<ICustomerReviewAppService>();
        _tenantManager = GetRequiredService<TenantManager>();
        _tenantRepository = GetRequiredService<ITenantRepository>();
        _membershipRepository = GetRequiredService<IRepository<Membership, Guid>>();
        _customerProfileRepository = GetRequiredService<IRepository<Eksabli.CustomerProfiles.CustomerProfile, Guid>>();
        _currentTenant = GetRequiredService<ICurrentTenant>();
        _currentPrincipalAccessor = GetRequiredService<ICurrentPrincipalAccessor>();
    }

    private IDisposable LoginAs(Guid userId)
    {
        var identity = new ClaimsIdentity("Test");
        identity.AddClaim(new Claim(AbpClaimTypes.UserId, userId.ToString()));
        return _currentPrincipalAccessor.Change(new ClaimsPrincipal(identity));
    }

    private async Task<Guid> CreateTenantAsync()
    {
        Guid tenantId = default;
        await WithUnitOfWorkAsync(async () =>
        {
            var tenant = await _tenantManager.CreateAsync("tenant-" + Guid.NewGuid().ToString("N"));
            await _tenantRepository.InsertAsync(tenant, autoSave: true);
            tenantId = tenant.Id;
        });
        return tenantId;
    }

    private async Task JoinAsync(Guid tenantId, Guid customerId)
    {
        await WithUnitOfWorkAsync(async () =>
        {
            using (_currentTenant.Change(tenantId))
            {
                await _membershipRepository.InsertAsync(Membership.Create(Guid.NewGuid(), customerId, DateTime.UtcNow), autoSave: true);
            }
        });
    }

    private async Task SetProfileNameAsync(Guid customerId, string firstName, string lastName)
    {
        await WithUnitOfWorkAsync(async () =>
        {
            var profile = Eksabli.CustomerProfiles.CustomerProfile.Create(Guid.NewGuid(), customerId);
            profile.SetName(firstName, lastName);
            await _customerProfileRepository.InsertAsync(profile, autoSave: true);
        });
    }

    [Fact]
    public async Task A_member_can_create_and_then_update_their_own_review()
    {
        var tenantId = await CreateTenantAsync();
        var customerId = Guid.NewGuid();
        await JoinAsync(tenantId, customerId);

        using (LoginAs(customerId))
        {
            var created = await WithUnitOfWorkAsync(() => _reviewService.CreateOrUpdateMyReviewAsync(tenantId, new CreateUpdateReviewDto
            {
                Rating = 4,
                Comment = "Pretty good.",
            }));
            created.Rating.ShouldBe(4);
            created.Comment.ShouldBe("Pretty good.");

            var updated = await WithUnitOfWorkAsync(() => _reviewService.CreateOrUpdateMyReviewAsync(tenantId, new CreateUpdateReviewDto
            {
                Rating = 5,
                Comment = "Actually, great!",
            }));

            // Same row edited in place, not a second review — see Review's own comment.
            updated.Id.ShouldBe(created.Id);
            updated.Rating.ShouldBe(5);
            updated.Comment.ShouldBe("Actually, great!");

            var mine = await WithUnitOfWorkAsync(() => _reviewService.GetMyReviewAsync(tenantId));
            mine.ShouldNotBeNull();
            mine!.Rating.ShouldBe(5);
        }
    }

    [Fact]
    public async Task A_customer_who_has_not_joined_the_business_cannot_leave_a_review()
    {
        var tenantId = await CreateTenantAsync();
        var stranger = Guid.NewGuid();

        using (LoginAs(stranger))
        {
            await Should.ThrowAsync<UserFriendlyException>(() => WithUnitOfWorkAsync(() =>
                _reviewService.CreateOrUpdateMyReviewAsync(tenantId, new CreateUpdateReviewDto { Rating = 3 })));
        }
    }

    [Fact]
    public async Task An_out_of_range_rating_is_refused()
    {
        var tenantId = await CreateTenantAsync();
        var customerId = Guid.NewGuid();
        await JoinAsync(tenantId, customerId);

        using (LoginAs(customerId))
        {
            await Should.ThrowAsync<AbpValidationException>(() => WithUnitOfWorkAsync(() =>
                _reviewService.CreateOrUpdateMyReviewAsync(tenantId, new CreateUpdateReviewDto { Rating = 6 })));
        }
    }

    [Fact]
    public async Task A_customer_can_delete_their_own_review()
    {
        var tenantId = await CreateTenantAsync();
        var customerId = Guid.NewGuid();
        await JoinAsync(tenantId, customerId);

        using (LoginAs(customerId))
        {
            await WithUnitOfWorkAsync(() => _reviewService.CreateOrUpdateMyReviewAsync(tenantId, new CreateUpdateReviewDto { Rating = 3 }));
            await WithUnitOfWorkAsync(() => _reviewService.DeleteMyReviewAsync(tenantId));

            (await WithUnitOfWorkAsync(() => _reviewService.GetMyReviewAsync(tenantId))).ShouldBeNull();
        }
    }

    [Fact]
    public async Task The_list_shows_the_average_rating_across_every_review_not_just_the_page()
    {
        var tenantId = await CreateTenantAsync();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var third = Guid.NewGuid();
        await JoinAsync(tenantId, first);
        await JoinAsync(tenantId, second);
        await JoinAsync(tenantId, third);

        using (LoginAs(first))
        {
            await WithUnitOfWorkAsync(() => _reviewService.CreateOrUpdateMyReviewAsync(tenantId, new CreateUpdateReviewDto { Rating = 5 }));
        }
        using (LoginAs(second))
        {
            await WithUnitOfWorkAsync(() => _reviewService.CreateOrUpdateMyReviewAsync(tenantId, new CreateUpdateReviewDto { Rating = 3 }));
        }
        using (LoginAs(third))
        {
            await WithUnitOfWorkAsync(() => _reviewService.CreateOrUpdateMyReviewAsync(tenantId, new CreateUpdateReviewDto { Rating = 1 }));
        }

        var page = await WithUnitOfWorkAsync(() => _reviewService.GetListAsync(tenantId, new PagedAndSortedResultRequestDto { MaxResultCount = 1 }));
        page.TotalCount.ShouldBe(3);
        page.Items.Count.ShouldBe(1);
        page.AverageRating.ShouldBe(3); // (5 + 3 + 1) / 3, not just the one item on this page
    }

    [Fact]
    public async Task A_reviewers_name_is_masked_to_first_name_and_last_initial()
    {
        var tenantId = await CreateTenantAsync();
        var customerId = Guid.NewGuid();
        await JoinAsync(tenantId, customerId);
        await SetProfileNameAsync(customerId, "Sara", "Khalil");

        using (LoginAs(customerId))
        {
            await WithUnitOfWorkAsync(() => _reviewService.CreateOrUpdateMyReviewAsync(tenantId, new CreateUpdateReviewDto { Rating = 4 }));
        }

        var page = await WithUnitOfWorkAsync(() => _reviewService.GetListAsync(tenantId, new PagedAndSortedResultRequestDto()));
        page.Items.Single().ReviewerName.ShouldBe("Sara K.");
    }

    [Fact]
    public async Task A_business_can_moderate_delete_a_review_left_on_its_own_page()
    {
        var tenantId = await CreateTenantAsync();
        var customerId = Guid.NewGuid();
        await JoinAsync(tenantId, customerId);

        Guid reviewId;
        using (LoginAs(customerId))
        {
            var review = await WithUnitOfWorkAsync(() => _reviewService.CreateOrUpdateMyReviewAsync(tenantId, new CreateUpdateReviewDto { Rating = 1, Comment = "Abusive content" }));
            reviewId = review.Id;
        }

        using (_currentTenant.Change(tenantId))
        {
            await WithUnitOfWorkAsync(() => _reviewService.ModerateDeleteAsync(reviewId));
        }

        var page = await WithUnitOfWorkAsync(() => _reviewService.GetListAsync(tenantId, new PagedAndSortedResultRequestDto()));
        page.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task A_review_left_at_one_business_does_not_appear_at_another()
    {
        var tenantA = await CreateTenantAsync();
        var tenantB = await CreateTenantAsync();
        var customerId = Guid.NewGuid();
        await JoinAsync(tenantA, customerId);
        await JoinAsync(tenantB, customerId);

        using (LoginAs(customerId))
        {
            await WithUnitOfWorkAsync(() => _reviewService.CreateOrUpdateMyReviewAsync(tenantA, new CreateUpdateReviewDto { Rating = 2 }));
        }

        var atB = await WithUnitOfWorkAsync(() => _reviewService.GetListAsync(tenantB, new PagedAndSortedResultRequestDto()));
        atB.TotalCount.ShouldBe(0);
    }
}
