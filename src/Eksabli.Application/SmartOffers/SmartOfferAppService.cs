using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Eksabli.Shared;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Entities;

namespace Eksabli.SmartOffers;

// Business Portal management. Authorization is applied on the controller per action (see SmartOffersController), in the
// same way OffersController does it.
[RemoteService(IsEnabled = false)]
public class SmartOfferAppService : SmartOfferServiceBase, ISmartOfferAppService
{
    private readonly ISmartOfferRepository _repository;

    public SmartOfferAppService(
        ISmartOfferRepository repository,
        IRepository<SmartOfferOrder, Guid> orderRepository,
        IRepository<SmartOfferInventory, Guid> inventoryRepository)
        : base(orderRepository, inventoryRepository)
    {
        _repository = repository;
    }

    public async Task<SmartOfferDto> GetAsync(Guid id)
    {
        var offer = await _repository.FindWithStagesAsync(id)
            ?? throw new EntityNotFoundException(typeof(SmartOffer), id);
        return await BuildDtoAsync(offer);
    }

    public async Task<PagedResultDto<SmartOfferDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var (offers, totalCount) = await _repository.GetListAsync(
            sorting: input.Sorting,
            skipCount: input.SkipCount,
            maxResultCount: input.MaxResultCount);

        var dtos = new List<SmartOfferDto>(offers.Count);
        foreach (var offer in offers)
        {
            dtos.Add(await BuildDtoAsync(offer));
        }

        return new PagedResultDto<SmartOfferDto>(totalCount, dtos);
    }

    public async Task<SmartOfferDto> CreateAsync(CreateUpdateSmartOfferDto input)
    {
        // A new offer has no existing stages, so every stage gets a fresh id regardless of what the client sent.
        var offer = SmartOffer.Create(
            GuidGenerator.Create(),
            ToDetails(input),
            ToPricing(input, existing: null),
            ToSchedule(input),
            input.IsEnabled);

        await _repository.InsertAsync(offer, autoSave: true);
        return await BuildDtoAsync(offer);
    }

    public async Task<SmartOfferDto> UpdateAsync(Guid id, CreateUpdateSmartOfferDto input)
    {
        var offer = await _repository.FindWithStagesAsync(id)
            ?? throw new EntityNotFoundException(typeof(SmartOffer), id);

        offer.Update(ToDetails(input), ToPricing(input, offer), ToSchedule(input), input.IsEnabled);

        await _repository.UpdateAsync(offer, autoSave: true);
        return await BuildDtoAsync(offer);
    }

    public async Task<SmartOfferDto> SetEnabledAsync(Guid id, SetSmartOfferEnabledDto input)
    {
        var offer = await _repository.FindWithStagesAsync(id)
            ?? throw new EntityNotFoundException(typeof(SmartOffer), id);

        offer.SetEnabled(input.IsEnabled);

        await _repository.UpdateAsync(offer, autoSave: true);
        return await BuildDtoAsync(offer);
    }

    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }

    private static SmartOfferDetails ToDetails(CreateUpdateSmartOfferDto input) =>
        new(input.TitleAr, input.TitleEn, input.DescriptionAr, input.DescriptionEn);

    private static SmartOfferScheduleSpec ToSchedule(CreateUpdateSmartOfferDto input) =>
        new(input.TimeZoneId, input.ValidFrom, input.ValidTo);

    // Stage ids: a stage that matches an existing one keeps its id (so pending orders stay attached), a new one gets a
    // fresh id, and an id that belongs to no stage on this offer is refused rather than silently re-created.
    private SmartOfferPricingSpec ToPricing(CreateUpdateSmartOfferDto input, SmartOffer? existing)
    {
        var stages = input.Stages.Select(stage =>
        {
            if (!MinuteOfDay.TryParse(stage.StartTime, out var start) || !MinuteOfDay.TryParse(stage.EndTime, out var end))
            {
                throw new UserFriendlyException("Each stage needs a valid start and end time (HH:mm).");
            }

            var id = ResolveStageId(stage, existing);
            return new SmartOfferStageSpec(id, start, end, stage.Price, stage.Currency, stage.QuantityLimit);
        }).ToList();

        return new SmartOfferPricingSpec(
            input.Strategy,
            input.Currency,
            input.BasePrice,
            input.MinimumPrice,
            input.DailyQuantity,
            stages);
    }

    private Guid ResolveStageId(CreateUpdateSmartOfferStageDto stage, SmartOffer? existing)
    {
        if (existing == null || !stage.Id.HasValue)
        {
            return GuidGenerator.Create();
        }

        if (existing.Stages.All(s => s.Id != stage.Id.Value))
        {
            throw new UserFriendlyException("One of the stages no longer exists. Reload the offer and try again.");
        }

        return stage.Id.Value;
    }

    // Builds the owner's view of an offer, including the live quote and what is left today. Computed in one place so the
    // Business Portal never works out a price or a remaining count itself.
    private async Task<SmartOfferDto> BuildDtoAsync(SmartOffer offer)
    {
        var nowUtc = NowUtc;
        var (today, _) = offer.ToLocal(nowUtc);
        var quote = offer.Quote(nowUtc);
        var nextChange = offer.GetNextChange(nowUtc);

        var stagesOrdered = offer.Stages.OrderBy(s => s.StartMinute).ToList();
        var slotIds = stagesOrdered.Select(s => s.Id).Append(offer.Id).ToList();
        var todaysStock = await InventoryRepository.GetListAsync(i => i.ServiceDate == today && slotIds.Contains(i.SlotId));
        var stockBySlot = todaysStock.ToDictionary(i => i.SlotId);

        var dto = ObjectMapper.Map<SmartOffer, SmartOfferDto>(offer);
        dto.Status = offer.GetStatus(nowUtc);
        dto.ServerNowUtc = nowUtc;
        dto.CurrentPrice = quote?.Price;
        dto.CurrentStageId = quote?.StageId;
        dto.NextChangeAtUtc = nextChange?.AtUtc;
        dto.NextPrice = nextChange?.Price;

        dto.RemainingNow = quote == null
            ? null
            : RemainingFor(stockBySlot.GetValueOrDefault(quote.SlotId), quote.QuantityLimit);

        dto.Stages = stagesOrdered.Select(stage => new SmartOfferStageDto
        {
            Id = stage.Id,
            StartTime = MinuteOfDay.Format(stage.StartMinute),
            EndTime = MinuteOfDay.Format(stage.EndMinute),
            Price = stage.Price,
            Currency = stage.Currency,
            QuantityLimit = stage.QuantityLimit,
            RemainingToday = RemainingFor(stockBySlot.GetValueOrDefault(stage.Id), stage.QuantityLimit),
        }).ToList();

        return dto;
    }
}
