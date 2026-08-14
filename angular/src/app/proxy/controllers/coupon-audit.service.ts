import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { CouponAuditFilterDto, CouponDto, CouponExcelDownloadDto } from '../rewards/models';
import type { DownloadTokenResultDto } from '../shared/models';

@Injectable({
  providedIn: 'root',
})
export class CouponAuditService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  getDownloadToken = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, DownloadTokenResultDto>({
      method: 'GET',
      url: '/api/app/coupon-audit/download-token',
    },
    { apiName: this.apiName,...config });
  

  getList = (input: CouponAuditFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<CouponDto>>({
      method: 'GET',
      url: '/api/app/coupon-audit',
      params: { status: input.status, branchId: input.branchId, membershipId: input.membershipId, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
  

  getListAsExcelFile = (input: CouponExcelDownloadDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, Blob>({
      method: 'GET',
      responseType: 'blob',
      url: '/api/app/coupon-audit/as-excel-file',
      params: { downloadToken: input.downloadToken, status: input.status, branchId: input.branchId, sorting: input.sorting },
    },
    { apiName: this.apiName,...config });
}