import { RestService, Rest } from '@abp/ng.core';
import type { PagedResultDto } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { BranchComparisonDto, CampaignPerformanceDto, CustomerSegmentReportDto, DashboardHomeDto, MemberGrowthPointDto, NotificationDeliveryRateDto, RedemptionRateReportDto, ReportPeriodDto, TransactionFilterDto, TransactionListItemDto, TierDistributionDto, TopCustomerDto, TransactionsExcelDownloadDto } from '../reports/models';
import type { DownloadTokenResultDto } from '../shared/models';

@Injectable({
  providedIn: 'root',
})
export class ReportsService {
  private restService = inject(RestService);
  apiName = 'Default';
  

  getBranchComparison = (input: ReportPeriodDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BranchComparisonDto[]>({
      method: 'GET',
      url: '/api/app/report/branch-comparison',
      params: { from: input.from, to: input.to },
    },
    { apiName: this.apiName,...config });
  

  getCampaignPerformance = (campaignId: string, config?: Partial<Rest.Config>) =>
    this.restService.request<any, CampaignPerformanceDto>({
      method: 'GET',
      url: `/api/app/report/campaign/${campaignId}/performance`,
    },
    { apiName: this.apiName,...config });
  

  getCustomerSegments = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, CustomerSegmentReportDto>({
      method: 'GET',
      url: '/api/app/report/customer-segments',
    },
    { apiName: this.apiName,...config });
  

  getDashboardHome = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, DashboardHomeDto>({
      method: 'GET',
      url: '/api/app/report/dashboard-home',
    },
    { apiName: this.apiName,...config });
  

  getMemberGrowth = (input: ReportPeriodDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, MemberGrowthPointDto[]>({
      method: 'GET',
      url: '/api/app/report/member-growth',
      params: { from: input.from, to: input.to },
    },
    { apiName: this.apiName,...config });
  

  getNotificationDeliveryRates = (input: ReportPeriodDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, NotificationDeliveryRateDto[]>({
      method: 'GET',
      url: '/api/app/report/notification-delivery-rates',
      params: { from: input.from, to: input.to },
    },
    { apiName: this.apiName,...config });
  

  getRedemptionRate = (input: ReportPeriodDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, RedemptionRateReportDto>({
      method: 'GET',
      url: '/api/app/report/redemption-rate',
      params: { from: input.from, to: input.to },
    },
    { apiName: this.apiName,...config });
  

  getTierDistribution = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, TierDistributionDto[]>({
      method: 'GET',
      url: '/api/app/report/tier-distribution',
    },
    { apiName: this.apiName,...config });
  

  getTopCustomers = (count: number = 10, config?: Partial<Rest.Config>) =>
    this.restService.request<any, TopCustomerDto[]>({
      method: 'GET',
      url: '/api/app/report/top-customers',
      params: { count },
    },
    { apiName: this.apiName,...config });
  

  getTransactionsAsExcelFile = (input: TransactionsExcelDownloadDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, Blob>({
      method: 'GET',
      responseType: 'blob',
      url: '/api/app/report/transactions/as-excel-file',
      params: { downloadToken: input.downloadToken, from: input.from, to: input.to },
    },
    { apiName: this.apiName,...config });
  

  getTransactionsDownloadToken = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, DownloadTokenResultDto>({
      method: 'GET',
      url: '/api/app/report/transactions/download-token',
    },
    { apiName: this.apiName,...config });


  getTransactionsList = (input: TransactionFilterDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PagedResultDto<TransactionListItemDto>>({
      method: 'GET',
      url: '/api/app/report/transactions',
      params: { type: input.type, branchId: input.branchId, staffId: input.staffId, membershipId: input.membershipId, from: input.from, to: input.to, sorting: input.sorting, skipCount: input.skipCount, maxResultCount: input.maxResultCount },
    },
    { apiName: this.apiName,...config });
}