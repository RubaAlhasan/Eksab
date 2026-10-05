import { describe, expect, it } from 'vitest';
import { Currency } from '../../proxy/shared/currency.enum';
import type { CustomerSmartOfferDto } from '../../proxy/smart-offers/models';
import { DEFAULT_DEAL_FILTERS, filterDeals, groupDeals, isEndingSoon, sortDeals } from './smart-deal-feed.util';

const NOW = Date.parse('2026-10-04T10:00:00Z');
const iso = (ms: number) => new Date(ms).toISOString();

let counter = 0;
const deal = (overrides: Partial<CustomerSmartOfferDto>): CustomerSmartOfferDto => ({
  id: overrides.id ?? `deal-${++counter}`,
  titleEn: 'Burger',
  titleAr: 'برغر',
  businessName: 'Cafe',
  currency: Currency.Usd,
  basePrice: 10,
  currentPrice: 5,
  discountPercent: 50,
  isAvailableNow: true,
  serverNowUtc: iso(NOW),
  currentStageEndsAtUtc: iso(NOW + 3 * 3600_000),
  remainingNow: 8,
  isMember: true,
  ...overrides,
});

describe('filterDeals', () => {
  it('keeps everything under the default filters', () => {
    const deals = [deal({ id: 'a' }), deal({ id: 'b', isAvailableNow: false })];
    expect(filterDeals(deals, DEFAULT_DEAL_FILTERS)).toHaveLength(2);
  });

  it('splits live from coming-up deals', () => {
    const deals = [deal({ id: 'live' }), deal({ id: 'later', isAvailableNow: false })];
    expect(filterDeals(deals, { ...DEFAULT_DEAL_FILTERS, availability: 'liveNow' }).map(d => d.id)).toEqual(['live']);
    expect(filterDeals(deals, { ...DEFAULT_DEAL_FILTERS, availability: 'comingUp' }).map(d => d.id)).toEqual(['later']);
  });

  it('filters by currency and by a minimum saving', () => {
    const deals = [
      deal({ id: 'usd-50', currency: Currency.Usd, discountPercent: 50 }),
      deal({ id: 'syp-10', currency: Currency.Syp, discountPercent: 10 }),
      deal({ id: 'none', discountPercent: null }),
    ];
    expect(filterDeals(deals, { ...DEFAULT_DEAL_FILTERS, currency: Currency.Syp }).map(d => d.id)).toEqual(['syp-10']);
    expect(filterDeals(deals, { ...DEFAULT_DEAL_FILTERS, minDiscount: 25 }).map(d => d.id)).toEqual(['usd-50']);
  });

  it('searches the title in either language and the business name, ignoring case and spacing', () => {
    const deals = [deal({ id: 'cafe', businessName: 'Corner Cafe' }), deal({ id: 'other', businessName: 'Grill House' })];
    expect(filterDeals(deals, { ...DEFAULT_DEAL_FILTERS, query: '  corner  ' }).map(d => d.id)).toEqual(['cafe']);
    expect(filterDeals(deals, { ...DEFAULT_DEAL_FILTERS, query: 'برغر' })).toHaveLength(2);
  });
});

describe('sortDeals', () => {
  it('puts the deal that ends soonest first, and a deal with no end time last', () => {
    const deals = [
      deal({ id: 'later', currentStageEndsAtUtc: iso(NOW + 5 * 3600_000) }),
      deal({ id: 'none', currentStageEndsAtUtc: null, nextChangeAtUtc: null }),
      deal({ id: 'soon', currentStageEndsAtUtc: iso(NOW + 600_000) }),
    ];
    expect(sortDeals(deals, 'endingSoon').map(d => d.id)).toEqual(['soon', 'later', 'none']);
  });

  it('orders by the biggest saving, then the lower price', () => {
    const deals = [
      deal({ id: 'pricey-half', discountPercent: 50, currentPrice: 5 }),
      deal({ id: 'cheap-half', discountPercent: 50, currentPrice: 2 }),
      deal({ id: 'quarter', discountPercent: 25, currentPrice: 7.5 }),
    ];
    expect(sortDeals(deals, 'biggestSaving').map(d => d.id)).toEqual(['cheap-half', 'pricey-half', 'quarter']);
  });

  it('orders by the lowest price', () => {
    const deals = [deal({ id: 'a', currentPrice: 9 }), deal({ id: 'b', currentPrice: 3 })];
    expect(sortDeals(deals, 'lowestPrice').map(d => d.id)).toEqual(['b', 'a']);
  });
});

describe('groupDeals', () => {
  it('lists a live deal ending within the hour under "ending soon" only', () => {
    const endingSoon = deal({ id: 'ending', currentStageEndsAtUtc: iso(NOW + 30 * 60_000) });
    const plenty = deal({ id: 'plenty', currentStageEndsAtUtc: iso(NOW + 4 * 3600_000) });

    const sections = groupDeals([endingSoon, plenty], NOW);

    expect(sections.endingSoon.map(d => d.id)).toEqual(['ending']);
    expect(sections.liveNow.map(d => d.id)).toEqual(['plenty']);
    expect(sections.comingUp).toEqual([]);
  });

  it('puts deals that are not on sale right now under "coming up"', () => {
    const sections = groupDeals([deal({ id: 'later', isAvailableNow: false, currentPrice: null })], NOW);
    expect(sections.comingUp.map(d => d.id)).toEqual(['later']);
    expect(sections.liveNow).toEqual([]);
  });

  it('does not count a stage that has already ended as ending soon', () => {
    expect(isEndingSoon(deal({ currentStageEndsAtUtc: iso(NOW - 60_000) }), NOW)).toBe(false);
  });
});
