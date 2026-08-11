import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { CampaignDto, CreateUpdateCampaignDto, TargetSegmentPreviewDto } from '../campaigns/models';

@Injectable({
  providedIn: 'root',
})
export class CampaignsService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  activate = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CampaignDto>({
      method: 'POST',
      url: `/api/app/campaign/${id}/activate`,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateUpdateCampaignDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CampaignDto>({
      method: 'POST',
      url: '/api/app/campaign',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/campaign/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CampaignDto>({
      method: 'GET',
      url: `/api/app/campaign/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<CampaignDto>>({
      method: 'GET',
      url: '/api/app/campaign',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  previewTargetSegment = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TargetSegmentPreviewDto>({
      method: 'GET',
      url: `/api/app/campaign/${id}/target-segment-preview`,
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateCampaignDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CampaignDto>({
      method: 'PUT',
      url: `/api/app/campaign/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}