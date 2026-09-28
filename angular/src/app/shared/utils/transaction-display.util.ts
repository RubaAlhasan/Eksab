import { PointsTransactionSource } from '../../proxy/wallets/points-transaction-source.enum';
import { PointsTransactionType } from '../../proxy/wallets/points-transaction-type.enum';

export function transactionTypeLabelKey(type: PointsTransactionType | undefined): string {
  switch (type) {
    case PointsTransactionType.Redeem:
      return '::Wallet:Transactions:TypeRedeem';
    case PointsTransactionType.Expire:
      return '::Wallet:Transactions:TypeExpire';
    case PointsTransactionType.Adjust:
      return '::Wallet:Transactions:TypeAdjust';
    case PointsTransactionType.Refund:
      return '::Wallet:Transactions:TypeRefund';
    default:
      return '::Wallet:Transactions:TypeEarn';
  }
}

export function transactionSourceLabelKey(source: PointsTransactionSource | undefined): string {
  switch (source) {
    case PointsTransactionSource.Campaign:
      return '::Wallet:Transactions:SourceCampaign';
    case PointsTransactionSource.Referral:
      return '::Wallet:Transactions:SourceReferral';
    case PointsTransactionSource.Birthday:
      return '::Wallet:Transactions:SourceBirthday';
    case PointsTransactionSource.Manual:
      return '::Wallet:Transactions:SourceManual';
    case PointsTransactionSource.Reward:
      return '::Wallet:Transactions:SourceReward';
    case PointsTransactionSource.Tier:
      return '::Wallet:Transactions:SourceTier';
    default:
      return '::Wallet:Transactions:SourcePurchase';
  }
}

/** `TransactionListItemDto.points` is already signed correctly at the source (confirmed by reading
 *  `PosAppService`/`PointsWallet`/`PointsExpirationWorker` — Redeem/Expire are written negative, Earn/
 *  Refund positive, Adjust carries whatever signed amount staff entered) — display just follows the
 *  real sign, no per-type inference needed. */
export function isCredit(points: number | undefined): boolean {
  return (points ?? 0) >= 0;
}
