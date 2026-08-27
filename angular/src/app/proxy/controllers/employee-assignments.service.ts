import { RestService, Rest } from '@abp/ng.core';
import type { PagedAndSortedResultRequestDto, PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { EmployeeAssignmentDto, InviteEmployeeDto, InviteEmployeeResultDto, UpdateEmployeeAssignmentDto } from '../employee-assignments/models';

@Injectable({
  providedIn: 'root',
})
export class EmployeeAssignmentsService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  getList = (input: PagedAndSortedResultRequestDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<EmployeeAssignmentDto>>({
      method: 'GET',
      url: '/api/app/employee-assignments',
      params: { sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  invite = (input: InviteEmployeeDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, InviteEmployeeResultDto>({
      method: 'POST',
      url: '/api/app/employee-assignments/invite',
      body: input,
    },
    { apiName: this.apiName,...config });
  

  remove = (id: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, void>({
      method: 'DELETE',
      url: `/api/app/employee-assignments/${id}`,
    },
    { apiName: this.apiName,...config });
  

  update = (id: string, input: UpdateEmployeeAssignmentDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, EmployeeAssignmentDto>({
      method: 'PUT',
      url: `/api/app/employee-assignments/${id}`,
      body: input,
    },
    { apiName: this.apiName,...config });
}