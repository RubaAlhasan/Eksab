import { Currency } from '../../proxy/shared/currency.enum';
import type { CustomerSmartOfferDto } from '../../proxy/smart-offers/models';

// Browsing helpers for the customer's deal feed. They filter and order what the server already returned: they never
// work out a price, a saving, or a window. Each rule reads a field the server computed (CustomerSmartOfferDto).

/** A deal whose stage ends within this many minutes is "ending soon". */
export const ENDING_SOON_MINUTES = 60;

export type DealAvailability = 'all' | 'liveNow' | 'comingUp';
export type DealSort = 'endingSoon' | 'biggestSaving' | 'lowestPrice';
export type MinDiscount = 0 | 10 | 25 | 50;

export interface DealFilters {
  query: string;
  availability: DealAvailability;
  /** Null means any currency. */
  currency: Currency | null;
  minDiscount: MinDiscount;
  sort: DealSort;
}

export const DEFAULT_DEAL_FILTERS: DealFilters = {
  query: '',
  availability: 'all',
  currency: null,
  minDiscount: 0,
  sort: 'endingSoon',
};

/** One titled group on the browse page, in display order. */
export interface DealSection {
  key: 'endingSoon' | 'liveNow' | 'comingUp';
  icon: string;
  titleKey: string;
  deals: CustomerSmartOfferDto[];
}

export interface DealSections {
  endingSoon: CustomerSmartOfferDto[];
  liveNow: CustomerSmartOfferDto[];
  comingUp: CustomerSmartOfferDto[];
}

function timeOf(iso: string | null | undefined): number | null {
  return iso ? new Date(iso).getTime() : null;
}

/** Sorts a missing time last, so a deal with no end time never jumps ahead of one that has one. */
function orderKey(value: number | null): number {
  return value ?? Number.MAX_SAFE_INTEGER;
}

export function isEndingSoon(deal: CustomerSmartOfferDto, nowMs: number): boolean {
  const endsAt = timeOf(deal.currentStageEndsAtUtc);
  return deal.isAvailableNow === true && endsAt !== null && endsAt > nowMs && endsAt - nowMs <= ENDING_SOON_MINUTES * 60_000;
}

export function filterDeals(deals: CustomerSmartOfferDto[], filters: DealFilters): CustomerSmartOfferDto[] {
  const query = filters.query.trim().toLowerCase();

  return deals.filter(deal => {
    if (filters.availability === 'liveNow' && !deal.isAvailableNow) return false;
    if (filters.availability === 'comingUp' && deal.isAvailableNow) return false;
    if (filters.currency !== null && deal.currency !== filters.currency) return false;
    if (filters.minDiscount > 0 && (deal.discountPercent ?? 0) < filters.minDiscount) return false;

    if (query) {
      const haystack = [deal.titleEn, deal.titleAr, deal.businessName].filter(Boolean).join(' ').toLowerCase();
      if (!haystack.includes(query)) return false;
    }

    return true;
  });
}

export function sortDeals(deals: CustomerSmartOfferDto[], sort: DealSort): CustomerSmartOfferDto[] {
  const copy = [...deals];

  switch (sort) {
    case 'biggestSaving':
      return copy.sort(
        (a, b) =>
          (b.discountPercent ?? 0) - (a.discountPercent ?? 0) ||
          orderKey(a.currentPrice ?? null) - orderKey(b.currentPrice ?? null),
      );
    case 'lowestPrice':
      return copy.sort((a, b) => orderKey(a.currentPrice ?? null) - orderKey(b.currentPrice ?? null));
    default:
      return copy.sort(
        (a, b) =>
          orderKey(timeOf(a.currentStageEndsAtUtc) ?? timeOf(a.nextChangeAtUtc)) -
          orderKey(timeOf(b.currentStageEndsAtUtc) ?? timeOf(b.nextChangeAtUtc)),
      );
  }
}

/**
 * Splits deals into the three groups the browse page shows. "Ending soon" is checked first, so a live deal that is about
 * to end is listed there and nowhere else.
 */
export function groupDeals(deals: CustomerSmartOfferDto[], nowMs: number): DealSections {
  const sections: DealSections = { endingSoon: [], liveNow: [], comingUp: [] };

  for (const deal of deals) {
    if (isEndingSoon(deal, nowMs)) {
      sections.endingSoon.push(deal);
    } else if (deal.isAvailableNow) {
      sections.liveNow.push(deal);
    } else {
      sections.comingUp.push(deal);
    }
  }

  return {
    endingSoon: sortDeals(sections.endingSoon, 'endingSoon'),
    liveNow: sortDeals(sections.liveNow, 'biggestSaving'),
    comingUp: sortDeals(sections.comingUp, 'endingSoon'),
  };
}

/** The server's clock at the last response, so grouping agrees with what the countdowns show. */
export function serverNowMs(deals: CustomerSmartOfferDto[]): number {
  return timeOf(deals.find(deal => deal.serverNowUtc)?.serverNowUtc) ?? Date.now();
}
