using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace Eksabli.Offers;

[RemoteService(IsEnabled = false)]
public class OfferAppService : ApplicationService, IOfferAppService
{
    private readonly IOfferRepository _repository;

    public OfferAppService(IOfferRepository repository)
    {
        _repository = repository;
    }

    public async Task<OfferDto> GetAsync(Guid id)
    {
        var offer = await _repository.GetAsync(id);
        return ObjectMapper.Map<Offer, OfferDto>(offer);
    }

    public async Task<PagedResultDto<OfferDto>> GetListAsync(PagedAndSortedResultRequestDto input)
    {
        var (offers, totalCount) = await _repository.GetListAsync(
            sorting: input.Sorting,
            skipCount: input.SkipCount,
            maxResultCount: input.MaxResultCount);

        return new PagedResultDto<OfferDto>(totalCount, ObjectMapper.Map<List<Offer>, List<OfferDto>>(offers));
    }

    public async Task<OfferDto> CreateAsync(CreateUpdateOfferDto input)
    {
        var offer = Offer.Create(GuidGenerator.Create(), input.TitleAr, input.TitleEn, input.StartDate, input.EndDate);
        ApplyInput(offer, input);
        await _repository.InsertAsync(offer);
        return ObjectMapper.Map<Offer, OfferDto>(offer);
    }

    public async Task<OfferDto> UpdateAsync(Guid id, CreateUpdateOfferDto input)
    {
        var offer = await _repository.GetAsync(id);
        offer.SetTitles(input.TitleAr, input.TitleEn);
        offer.SetDateRange(input.StartDate, input.EndDate);
        ApplyInput(offer, input);
        await _repository.UpdateAsync(offer);
        return ObjectMapper.Map<Offer, OfferDto>(offer);
    }

    public async Task DeleteAsync(Guid id)
    {
        await _repository.DeleteAsync(id);
    }

    private static void ApplyInput(Offer offer, CreateUpdateOfferDto input)
    {
        offer.SetBranch(input.BranchId);
        offer.SetDescriptions(input.DescriptionAr, input.DescriptionEn);
        offer.SetImageBlobName(input.ImageBlobName);
    }
}
