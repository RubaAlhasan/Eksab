import type { EntityDto, FullAuditedEntityDto } from '@abp/ng.core';
import type { CampaignType } from './campaign-type.enum';
import type { CampaignStatus } from './campaign-status.enum';
import type { CampaignTargetRuleSegmentType } from './campaign-target-rule-segment-type.enum';

export interface CampaignDto extends FullAuditedEntityDto<string> {
  tenantId?: string | null;
  nameAr?: string;
  nameEn?: string;
  type?: CampaignType;
  rulesJson?: string | null;
  startDate?: string;
  endDate?: string;
  status?: CampaignStatus;
  targetRules?: CampaignTargetRuleDto[];
}

export interface CampaignTargetRuleDto extends EntityDto<string> {
  campaignId?: string;
  segmentType?: CampaignTargetRuleSegmentType;
  parametersJson?: string | null;
}

export interface CreateUpdateCampaignDto {
  nameAr: string;
  nameEn: string;
  type: CampaignType;
  rulesJson?: string | null;
  startDate: string;
  endDate: string;
  targetRules?: CreateUpdateCampaignTargetRuleDto[];
}

export interface CreateUpdateCampaignTargetRuleDto {
  segmentType: CampaignTargetRuleSegmentType;
  parametersJson?: string | null;
}

export interface TargetSegmentPreviewDto {
  matchedMembershipCount?: number;
}
