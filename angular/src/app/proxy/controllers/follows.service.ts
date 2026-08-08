import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { FollowDto } from '../engagement/models';

@Injectable({
  providedIn: 'root',
})
export class FollowsService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  follow = (tenantId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'POST',
      url: `/api/app/follow/${tenantId}`,
    },
    { apiName: this.apiName,...config });
  

  getFollowers = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<FollowDto>>({
      method: 'GET',
      url: '/api/app/follow/followers',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getMyFollows = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, FollowDto[]>({
      method: 'GET',
      url: '/api/app/follow/my',
    },
    { apiName: this.apiName,...config });
  

  unfollow = (tenantId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/follow/${tenantId}`,
    },
    { apiName: this.apiName,...config });
}