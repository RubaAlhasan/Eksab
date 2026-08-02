using Riok.Mapperly.Abstractions;
using Volo.Abp.Mapperly;
using Eksabli.Authors;
using Eksabli.Books;
using Eksabli.BusinessProfiles;
using Eksabli.Branches;
using Eksabli.CustomerProfiles;
using Eksabli.EmployeeAssignments;
using Eksabli.Devices;
using Eksabli.Memberships;
using Eksabli.Wallets;
using Eksabli.Rewards;
using Eksabli.Billing;
using Eksabli.Campaigns;
using Eksabli.Offers;
using Eksabli.Notifications;
using Eksabli.Engagement;
using Eksabli.Platform;

namespace Eksabli;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class EksabliBookToBookDtoMapper : MapperBase<Book, BookDto>
{
    [MapperIgnoreTarget(nameof(BookDto.AuthorName))]
    public override partial BookDto Map(Book source);

    [MapperIgnoreTarget(nameof(BookDto.AuthorName))]
    public override partial void Map(Book source, BookDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class EksabliCreateUpdateBookDtoToBookMapper : MapperBase<CreateUpdateBookDto, Book>
{
    public override partial Book Map(CreateUpdateBookDto source);

    public override partial void Map(CreateUpdateBookDto source, Book destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class EksabliAuthorToAuthorDtoMapper : MapperBase<Author, AuthorDto>
{
    public override partial AuthorDto Map(Author source);

    public override partial void Map(Author source, AuthorDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class EksabliCreateUpdateAuthorDtoToAuthorMapper : MapperBase<CreateUpdateAuthorDto, Author>
{
    public override partial Author Map(CreateUpdateAuthorDto source);

    public override partial void Map(CreateUpdateAuthorDto source, Author destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class EksabliAuthorToAuthorExcelDtoMapper : MapperBase<Author, AuthorExcelDto>
{
    public override partial AuthorExcelDto Map(Author source);

    public override partial void Map(Author source, AuthorExcelDto destination);
}

// The 5 entities below are rich models (private setters, behavior methods) — unlike Book/Author,
// app-service code calls constructors/behavior methods directly for creates/updates rather than
// ObjectMapper.Map<CreateDto, Entity>(), so only the read direction (Entity -> Dto) is mapped here.

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class EksabliBusinessProfileToBusinessProfileDtoMapper : MapperBase<BusinessProfile, BusinessProfileDto>
{
    public override partial BusinessProfileDto Map(BusinessProfile source);

    public override partial void Map(BusinessProfile source, BusinessProfileDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class EksabliBranchToBranchDtoMapper : MapperBase<Branch, BranchDto>
{
    public override partial BranchDto Map(Branch source);

    public override partial void Map(Branch source, BranchDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class EksabliCustomerProfileToCustomerProfileDtoMapper : MapperBase<CustomerProfile, CustomerProfileDto>
{
    public override partial CustomerProfileDto Map(CustomerProfile source);

    public override partial void Map(CustomerProfile source, CustomerProfileDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class EksabliEmployeeAssignmentToEmployeeAssignmentDtoMapper : MapperBase<EmployeeAssignment, EmployeeAssignmentDto>
{
    [MapperIgnoreTarget(nameof(EmployeeAssignmentDto.UserEmail))]
    public override partial EmployeeAssignmentDto Map(EmployeeAssignment source);

    [MapperIgnoreTarget(nameof(EmployeeAssignmentDto.UserEmail))]
    public override partial void Map(EmployeeAssignment source, EmployeeAssignmentDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class EksabliDeviceToDeviceDtoMapper : MapperBase<Device, DeviceDto>
{
    public override partial DeviceDto Map(Device source);

    public override partial void Map(Device source, DeviceDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class EksabliMembershipToMembershipDtoMapper : MapperBase<Membership, MembershipDto>
{
    public override partial MembershipDto Map(Membership source);

    public override partial void Map(Membership source, MembershipDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class EksabliPointsWalletToPointsWalletDtoMapper : MapperBase<PointsWallet, PointsWalletDto>
{
    [MapperIgnoreTarget(nameof(PointsWalletDto.CurrentTierName))]
    public override partial PointsWalletDto Map(PointsWallet source);

    [MapperIgnoreTarget(nameof(PointsWalletDto.CurrentTierName))]
    public override partial void Map(PointsWallet source, PointsWalletDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class EksabliPointsTransactionToPointsTransactionDtoMapper : MapperBase<PointsTransaction, PointsTransactionDto>
{
    public override partial PointsTransactionDto Map(PointsTransaction source);

    public override partial void Map(PointsTransaction source, PointsTransactionDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class EksabliTierToTierDtoMapper : MapperBase<Tier, TierDto>
{
    public override partial TierDto Map(Tier source);

    public override partial void Map(Tier source, TierDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class EksabliPointRuleToPointRuleDtoMapper : MapperBase<PointRule, PointRuleDto>
{
    public override partial PointRuleDto Map(PointRule source);

    public override partial void Map(PointRule source, PointRuleDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class EksabliRewardToRewardDtoMapper : MapperBase<Reward, RewardDto>
{
    public override partial RewardDto Map(Reward source);

    public override partial void Map(Reward source, RewardDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class EksabliCouponToCouponDtoMapper : MapperBase<Coupon, CouponDto>
{
    [MapperIgnoreTarget(nameof(CouponDto.RewardNameAr))]
    [MapperIgnoreTarget(nameof(CouponDto.RewardNameEn))]
    public override partial CouponDto Map(Coupon source);

    [MapperIgnoreTarget(nameof(CouponDto.RewardNameAr))]
    [MapperIgnoreTarget(nameof(CouponDto.RewardNameEn))]
    public override partial void Map(Coupon source, CouponDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class EksabliSubscriptionPlanToSubscriptionPlanDtoMapper : MapperBase<SubscriptionPlan, SubscriptionPlanDto>
{
    public override partial SubscriptionPlanDto Map(SubscriptionPlan source);

    public override partial void Map(SubscriptionPlan source, SubscriptionPlanDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class EksabliTenantSubscriptionToTenantSubscriptionDtoMapper : MapperBase<TenantSubscription, TenantSubscriptionDto>
{
    [MapperIgnoreTarget(nameof(TenantSubscriptionDto.PlanName))]
    public override partial TenantSubscriptionDto Map(TenantSubscription source);

    [MapperIgnoreTarget(nameof(TenantSubscriptionDto.PlanName))]
    public override partial void Map(TenantSubscription source, TenantSubscriptionDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class EksabliInvoiceToInvoiceDtoMapper : MapperBase<Invoice, InvoiceDto>
{
    public override partial InvoiceDto Map(Invoice source);

    public override partial void Map(Invoice source, InvoiceDto destination);
}

// TargetRules is mapped manually in CampaignAppService (source is IReadOnlyCollection<CampaignTargetRule>,
// only populated when the aggregate was loaded via ICampaignRepository.WithDetailsAsync).
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class EksabliCampaignToCampaignDtoMapper : MapperBase<Campaign, CampaignDto>
{
    [MapperIgnoreTarget(nameof(CampaignDto.TargetRules))]
    public override partial CampaignDto Map(Campaign source);

    [MapperIgnoreTarget(nameof(CampaignDto.TargetRules))]
    public override partial void Map(Campaign source, CampaignDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class EksabliCampaignTargetRuleToCampaignTargetRuleDtoMapper : MapperBase<CampaignTargetRule, CampaignTargetRuleDto>
{
    public override partial CampaignTargetRuleDto Map(CampaignTargetRule source);

    public override partial void Map(CampaignTargetRule source, CampaignTargetRuleDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class EksabliOfferToOfferDtoMapper : MapperBase<Offer, OfferDto>
{
    public override partial OfferDto Map(Offer source);

    public override partial void Map(Offer source, OfferDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class EksabliNotificationToNotificationDtoMapper : MapperBase<Notification, NotificationDto>
{
    public override partial NotificationDto Map(Notification source);

    public override partial void Map(Notification source, NotificationDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class EksabliReferralToReferralDtoMapper : MapperBase<Referral, ReferralDto>
{
    public override partial ReferralDto Map(Referral source);

    public override partial void Map(Referral source, ReferralDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class EksabliAchievementToAchievementDtoMapper : MapperBase<Achievement, AchievementDto>
{
    public override partial AchievementDto Map(Achievement source);

    public override partial void Map(Achievement source, AchievementDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class EksabliAchievementAwardToAchievementAwardDtoMapper : MapperBase<AchievementAward, AchievementAwardDto>
{
    public override partial AchievementAwardDto Map(AchievementAward source);

    public override partial void Map(AchievementAward source, AchievementAwardDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class EksabliFollowToFollowDtoMapper : MapperBase<Follow, FollowDto>
{
    public override partial FollowDto Map(Follow source);

    public override partial void Map(Follow source, FollowDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class EksabliCategoryToCategoryDtoMapper : MapperBase<Category, CategoryDto>
{
    public override partial CategoryDto Map(Category source);

    public override partial void Map(Category source, CategoryDto destination);
}

// Messages is mapped manually in SupportTicketAppService (source is IReadOnlyCollection<SupportTicketMessage>,
// only meaningful when the aggregate was loaded via ISupportTicketRepository.WithDetailsAsync).
[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class EksabliSupportTicketToSupportTicketDtoMapper : MapperBase<SupportTicket, SupportTicketDto>
{
    [MapperIgnoreTarget(nameof(SupportTicketDto.Messages))]
    public override partial SupportTicketDto Map(SupportTicket source);

    [MapperIgnoreTarget(nameof(SupportTicketDto.Messages))]
    public override partial void Map(SupportTicket source, SupportTicketDto destination);
}

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public partial class EksabliSupportTicketMessageToSupportTicketMessageDtoMapper : MapperBase<SupportTicketMessage, SupportTicketMessageDto>
{
    public override partial SupportTicketMessageDto Map(SupportTicketMessage source);

    public override partial void Map(SupportTicketMessage source, SupportTicketMessageDto destination);
}
