import { RestService, Rest } from '@abp/ng.core';
import { Injectable, inject } from '@angular/core';
import type { BusinessDashboardSummaryDto, BusinessDashboardTrendPointDto, BusinessInsightDto, BusinessOfferPerformanceDto, DashboardRangeDto, PeakHourCellDto } from '../dashboards/models';

@Injectable({
  providedIn: 'root',
})
export class BusinessDashboardService {
  private restService = inject(RestService);
  apiName = 'Default';


  getInsights = (config?: Partial<Rest.Config>) =>
    this.restService.request<any, BusinessInsightDto[]>({
      method: 'GET',
      url: '/api/app/business-dashboard/insights',
    },
    { apiName: this.apiName,...config });


  getOfferPerformance = (input: DashboardRangeDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BusinessOfferPerformanceDto[]>({
      method: 'GET',
      url: '/api/app/business-dashboard/offers',
      params: { from: input.from, to: input.to },
    },
    { apiName: this.apiName,...config });


  getPeakHours = (input: DashboardRangeDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, PeakHourCellDto[]>({
      method: 'GET',
      url: '/api/app/business-dashboard/peak-hours',
      params: { from: input.from, to: input.to },
    },
    { apiName: this.apiName,...config });


  getSummary = (input: DashboardRangeDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BusinessDashboardSummaryDto>({
      method: 'GET',
      url: '/api/app/business-dashboard/summary',
      params: { from: input.from, to: input.to },
    },
    { apiName: this.apiName,...config });


  getTrends = (input: DashboardRangeDto, config?: Partial<Rest.Config>) =>
    this.restService.request<any, BusinessDashboardTrendPointDto[]>({
      method: 'GET',
      url: '/api/app/business-dashboard/trends',
      params: { from: input.from, to: input.to },
    },
    { apiName: this.apiName,...config });

}
