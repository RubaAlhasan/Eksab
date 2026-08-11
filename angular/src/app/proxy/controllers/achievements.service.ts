import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { AchievementAwardDto, AchievementDto, AwardAchievementDto, CreateUpdateAchievementDto } from '../engagement/models';

@Injectable({
  providedIn: 'root',
})
export class AchievementsService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  award = (input: AwardAchievementDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AchievementAwardDto>({
      method: 'POST',
      url: '/api/app/achievement/award',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateUpdateAchievementDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AchievementDto>({
      method: 'POST',
      url: '/api/app/achievement',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  delete = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/achievement/${id}`,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AchievementDto>({
      method: 'GET',
      url: `/api/app/achievement/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getAwardsForMembership = (membershipId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AchievementAwardDto[]>({
      method: 'GET',
      url: `/api/app/achievement/membership/${membershipId}/awards`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<AchievementDto>>({
      method: 'GET',
      url: '/api/app/achievement',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: CreateUpdateAchievementDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, AchievementDto>({
      method: 'PUT',
      url: `/api/app/achievement/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}