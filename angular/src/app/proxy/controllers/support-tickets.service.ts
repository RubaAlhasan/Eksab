import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { AddSupportTicketMessageDto, CreateSupportTicketDto, SupportTicketDto, SupportTicketFilterDto, SupportTicketMessageDto } from '../platform/models';

@Injectable({
  providedIn: 'root',
})
export class SupportTicketsService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  addMessage = (id: string, input: AddSupportTicketMessageDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SupportTicketMessageDto>({
      method: 'POST',
      url: `/api/app/support-ticket/${id}/messages`,
      body: input,
    },
    { apiName: this.apiName,...config });
  

  create = (input: CreateSupportTicketDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SupportTicketDto>({
      method: 'POST',
      url: '/api/app/support-ticket',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  get = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SupportTicketDto>({
      method: 'GET',
      url: `/api/app/support-ticket/${id}`,
    },
    { apiName: this.apiName,...config });
  

  getList = (input: SupportTicketFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<SupportTicketDto>>({
      method: 'GET',
      url: '/api/app/support-ticket',
      params: { status: input.status, priority: input.priority, tenantId: input.tenantId, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  resolve = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, SupportTicketDto>({
      method: 'POST',
      url: `/api/app/support-ticket/${id}/resolve`,
    },
    { apiName: this.apiName,...config });
}