import { CampaignType } from '../../proxy/campaigns/campaign-type.enum';

/** Deterministic per-type emoji, same "no per-item image exists" reasoning as reward-display.util.ts's
 *  rewardTypeEmoji — campaigns have no image field either. */
export function campaignTypeEmoji(type: CampaignType | undefined): string {
  switch (type) {
    case CampaignType.Birthday:
      return '🎂';
    case CampaignType.DoublePoints:
      return '✨';
    case CampaignType.SpendXGetY:
      return '🛍️';
    case CampaignType.WinBack:
      return '👋';
    case CampaignType.Vip:
      return '👑';
    case CampaignType.NewCustomer:
      return '🎉';
    case CampaignType.Referral:
      return '🤝';
    default:
      return '📣';
  }
}

export function campaignTypeLabelKey(type: CampaignType | undefined): string {
  switch (type) {
    case CampaignType.Birthday:
      return '::Wallet:Campaigns:TypeBirthday';
    case CampaignType.DoublePoints:
      return '::Wallet:Campaigns:TypeDoublePoints';
    case CampaignType.SpendXGetY:
      return '::Wallet:Campaigns:TypeSpendXGetY';
    case CampaignType.WinBack:
      return '::Wallet:Campaigns:TypeWinBack';
    case CampaignType.Vip:
      return '::Wallet:Campaigns:TypeVip';
    case CampaignType.NewCustomer:
      return '::Wallet:Campaigns:TypeNewCustomer';
    case CampaignType.Referral:
      return '::Wallet:Campaigns:TypeReferral';
    default:
      return '::Wallet:Campaigns:TypeOther';
  }
}
